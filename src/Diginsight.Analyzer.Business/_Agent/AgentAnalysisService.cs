﻿using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AgentAnalysisService : IAgentAnalysisService
{
    private static readonly AnalysisException NotPendingException =
        new ("Execution is not pending", HttpStatusCode.Conflict, "NotPending");

    private readonly ILogger logger;
    private readonly IAgentExecutionService executionService;
    private readonly IInternalAnalysisService internalAnalysisService;
    private readonly ISnapshotService snapshotService;
    private readonly IAmbientService ambientService;
    private readonly IPluginService pluginService;
    private readonly IPermissionService permissionService;
    private readonly IAnalysisFileRepository fileRepository;
    private readonly ICoreOptions coreOptions;

    public AgentAnalysisService(
        ILogger<AgentAnalysisService> logger,
        IAgentExecutionService executionService,
        IInternalAnalysisService internalAnalysisService,
        ISnapshotService snapshotService,
        IAmbientService ambientService,
        IPluginService pluginService,
        IPermissionService permissionService,
        IAnalysisFileRepository fileRepository,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.logger = logger;
        this.executionService = executionService;
        this.internalAnalysisService = internalAnalysisService;
        this.snapshotService = snapshotService;
        this.ambientService = ambientService;
        this.pluginService = pluginService;
        this.permissionService = permissionService;
        this.fileRepository = fileRepository;
        this.coreOptions = coreOptions.Value;
    }

    public async Task<ExtendedAnalysisCoord> AnalyzeAsync(
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        EncodedStream definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        CancellationToken cancellationToken
    )
    {
        LogMessages.PreparingForStart(logger);

        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos = await internalAnalysisService.CalculateStepsAsync(steps, cancellationToken);

        Guid analysisId = ambientService.NewUlid();
        AnalysisCoord coord = new (analysisId);

        Guid executionId = await StartAsync(coord, globalMeta, stepExecutorProtos, progress, null, null, definitionStream, inputPayloads, cancellationToken);

        return new ExtendedAnalysisCoord(executionId, analysisId);
    }

    public async Task<ExtendedAnalysisCoord> ReattemptAsync(
        Guid analysisId,
        GlobalMeta? globalMeta,
        EncodedStream? definitionStream,
        CancellationToken cancellationToken
    )
    {
        LogMessages.PreparingForReattempt(logger, analysisId);

        if (await snapshotService.GetAnalysesAE(analysisId, true, cancellationToken).FirstOrDefaultAsync(cancellationToken) is not { } snapshot)
        {
            throw AnalysisExceptions.NoSuchAnalysis;
        }

        await permissionService.CheckCanInvokeAnalysisAsync(snapshot.PermissionAssignments, cancellationToken);

        if (snapshot.FinishedAt is null)
        {
            throw AnalysisExceptions.AlreadyPendingOrRunning;
        }

        int attempt = snapshot.Attempt + 1;
        AnalysisCoord coord = snapshot.AnalysisCoord with { Attempt = attempt };

        GlobalMeta finalGlobalMeta = snapshot.GlobalMeta.WithOverwrite(globalMeta);
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos = await internalAnalysisService.CalculateStepsAsync(snapshot.Steps, cancellationToken);

        Guid executionId = await StartAsync(coord, finalGlobalMeta, stepExecutorProtos, snapshot.Progress!, null, null, definitionStream, [ ], cancellationToken);

        return new ExtendedAnalysisCoord(executionId, analysisId, attempt);
    }

    public async Task<AnalysisCoord> DequeueAsync(Guid executionId, CancellationToken cancellationToken)
    {
        LogMessages.PreparingForDequeue(logger, executionId);

        if (await snapshotService.GetAnalysisAsync(executionId, true, false,cancellationToken) is not { } snapshot)
        {
            throw AnalysisExceptions.NoSuchExecution;
        }

        await permissionService.CheckCanDequeueExecutionAsync(snapshot.PermissionAssignments, cancellationToken);

        if (snapshot.StartedAt is not null)
        {
            throw NotPendingException;
        }

        GlobalMeta globalMeta = snapshot.GlobalMeta;
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos = await internalAnalysisService.CalculateStepsAsync(snapshot.Steps, cancellationToken);

        AnalysisCoord coord = snapshot.AnalysisCoord;
        _ = await StartAsync(
            coord,
            globalMeta,
            stepExecutorProtos,
            snapshot.Progress!,
            executionId,
            snapshot.QueuedAt,
            null,
            [ ],
            cancellationToken
        );

        return coord;
    }

    public async Task<ExtendedAnalysisCoord?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        AnalysisCoord coord;
        return await executionService.GetCurrentAsync(cancellationToken) is ({ Kind: ExecutionKind.Analysis, Id: var executionId }, var detail)
            ? new ExtendedAnalysisCoord(executionId, (coord = (AnalysisCoord)detail).Id, coord.Attempt)
            : null;
    }

    public IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionsAE(CancellationToken cancellationToken)
        => CoreAbortExecutionAE(null, cancellationToken);

    public IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionAE(Guid executionId, CancellationToken cancellationToken)
        => CoreAbortExecutionAE(executionId, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IAsyncEnumerable<ExtendedAnalysisCoord> CoreAbortExecutionAE(Guid? executionId, CancellationToken cancellationToken)
    {
        async Task CheckCanAbortAsync(ExecutionKind executionKind, Guid executionId0, object detail, CancellationToken ct)
        {
            AnalysisContextSnapshot snapshot = (await snapshotService.GetAnalysisAsync(executionId0, false, false, ct))!;
            await permissionService.CheckCanInvokeAnalysisAsync(snapshot.PermissionAssignments, ct);
        }

        return executionService
            .AbortAE(ExecutionKind.Analysis, executionId, CheckCanAbortAsync, cancellationToken)
            .Select(
                static x =>
                {
                    (Guid analysisId, int attempt) = (AnalysisCoord)x.Detail;
                    return new ExtendedAnalysisCoord(x.Id, analysisId, attempt);
                }
            );
    }

    public IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysesAE(Guid analysisId, CancellationToken cancellationToken)
    {
        return snapshotService.GetAnalysesAE(analysisId, false, cancellationToken)
            .Where(static x => x is { StartedAt: not null, FinishedAt: null })
            .SelectManyAwaitWithCancellation((x, ct) => ValueTask.FromResult(CoreAbortExecutionAE(x.ExecutionId, ct)));
    }

    public async IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysisAE(AnalysisCoord coord, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (await snapshotService.GetAnalysisAsync(coord, false, false,cancellationToken) is not { StartedAt: not null, FinishedAt: null, ExecutionId: var executionId })
        {
            yield break;
        }

        await foreach (ExtendedAnalysisCoord coord0 in CoreAbortExecutionAE(executionId, cancellationToken))
        {
            yield return coord0;
        }
    }

    private Task<Guid> StartAsync(
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos,
        JObject progress,
        Guid? requestedExecutionId,
        DateTime? queuedAt,
        EncodedStream? definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        CancellationToken cancellationToken
    )
    {
        return executionService.StartAsync<AnalysisLease>(
            ExecutionKind.Analysis,
            requestedExecutionId,
            coord,
            lease => internalAnalysisService.FillLease(lease, coord),
            (lease, ct) => internalAnalysisService.HasConflictAsync(lease, stepExecutorProtos, ct),
            (executionId, ct) => CoreStartAsync(executionId, coord, queuedAt, globalMeta, stepExecutorProtos, progress, definitionStream, inputPayloads, ct),
            cancellationToken
        );
    }

    private async Task CoreStartAsync(
        Guid executionId,
        AnalysisCoord coord,
        DateTime? queuedAt,
        GlobalMeta globalMeta,
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos,
        JObject progress,
        EncodedStream? definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        CancellationToken cancellationToken
    )
    {
        int parallelism = globalMeta.Parallelism ?? coreOptions.DefaultParallelism;
        Guid analysisId = coord.Id;

        if (queuedAt is not null)
        {
            try
            {
                await fileRepository.MoveQueuedTreeAsync(executionId, coord, cancellationToken);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
            {
                await fileRepository.DeleteQueuedTreeAsync(executionId);
                throw;
            }
        }
        else
        {
            try
            {
                if (definitionStream is not null)
                {
                    await fileRepository.WriteDefinitionAsync(definitionStream, coord, cancellationToken);
                }
                await fileRepository.WriteProgressAsync(progress, coord, cancellationToken);

                await Parallel.ForEachAsync(
                    inputPayloads,
                    new ParallelOptions() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = parallelism },
                    async (inputPayload, ct) =>
                    {
                        string label = inputPayload.Label;
                        LogMessages.WritingInputPayload(logger, label, analysisId);
                        await fileRepository.WriteInputPayloadAsync(inputPayload, analysisId, label, ct);
                    }
                );
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
            {
                await fileRepository.DeleteTreeAsync(coord);
                throw;
            }
        }

        await executionService.RunDetachedAsync(
            (sp, disposables) =>
            {
                IEnumerable<IAnalyzerStepExecutor> stepExecutors = stepExecutorProtos
                    .Select(
                        x =>
                        {
                            IAnalyzerStepExecutor stepExecutor = x.Step.CreateExecutor(sp, x.Inputs);
                            // ReSharper disable once SuspiciousTypeConversion.Global
                            if (stepExecutor is IDisposable disposable)
                            {
                                disposables.Add(disposable);
                            }
                            return stepExecutor;
                        }
                    )
                    .ToArray();

                IEnumerable<IEventSender> pluginEventSenders = pluginService.CreateEventSenders(sp);
                // ReSharper disable once SuspiciousTypeConversion.Global
                disposables.AddRange(pluginEventSenders.OfType<IDisposable>());

                return (
                    stepExecutors,
                    pluginEventSenders.Concat(sp.GetServices<IEventSender>()).ToArray(),
                    sp.GetRequiredService<IAnalysisExecutor>()
                );
            },
            (services, ct) =>
            {
                var (stepExecutors, eventSenders, analysisExecutor) = services;

                return analysisExecutor
                    .ExecuteAsync(
                        executionId,
                        coord,
                        queuedAt,
                        globalMeta,
                        stepExecutors,
                        eventSenders,
                        progress,
                        ct
                    );
            }
        );
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Preparing for start analysis")]
        internal static partial void PreparingForStart(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Preparing for reattempt analysis '{AnalysisId}'")]
        internal static partial void PreparingForReattempt(ILogger logger, Guid analysisId);

        [LoggerMessage(2, LogLevel.Debug, "Preparing for dequeue execution '{ExecutionId}'")]
        internal static partial void PreparingForDequeue(ILogger logger, Guid executionId);

        [LoggerMessage(3, LogLevel.Debug, "Writing input payload '{Label}' for analysis '{AnalysisId}'")]
        internal static partial void WritingInputPayload(ILogger logger, string label, Guid analysisId);
    }
}
