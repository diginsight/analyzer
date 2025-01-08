using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorAnalysisService : IOrchestratorAnalysisService
{
    private readonly IOrchestratorExecutionService executionService;
    private readonly IInternalAnalysisService internalAnalysisService;
    private readonly IOrchestratorAnalysisContextFactory analysisContextFactory;
    private readonly ISnapshotService snapshotService;
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly IAnalysisFileRepository fileRepository;
    private readonly IAmbientService ambientService;
    private readonly IOptionsMonitor<CoreOptions> coreOptionsMonitor;
    private readonly JsonSerializer jsonSerializer;

    private IOrchestratorCoreOptions CoreOptions => coreOptionsMonitor.CurrentValue;

    public OrchestratorAnalysisService(
        IOrchestratorExecutionService executionService,
        IInternalAnalysisService internalAnalysisService,
        IOrchestratorAnalysisContextFactory analysisContextFactory,
        ISnapshotService snapshotService,
        IAnalysisInfoRepository infoRepository,
        IAnalysisFileRepository fileRepository,
        IAmbientService ambientService,
        IOptionsMonitor<CoreOptions> coreOptionsMonitor,
        JsonSerializer jsonSerializer
    )
    {
        this.executionService = executionService;
        this.internalAnalysisService = internalAnalysisService;
        this.analysisContextFactory = analysisContextFactory;
        this.snapshotService = snapshotService;
        this.infoRepository = infoRepository;
        this.fileRepository = fileRepository;
        this.ambientService = ambientService;
        this.coreOptionsMonitor = coreOptionsMonitor;
        this.jsonSerializer = jsonSerializer;
    }

    public async Task<QueuableAnalysisCoord> AnalyzeAsync(
        GlobalMeta globalMeta,
        IEnumerable<StepInstance> steps,
        JObject progress,
        EncodedStream definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        string? agentPool,
        QueuingPolicy queuingPolicy,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos = await internalAnalysisService.CalculateStepsAsync(steps, cancellationToken);

        return await StartAsync(
            globalMeta,
            stepExecutorProtos,
            progress,
            async (ac, ct) =>
            {
                await using Stream progressStream = new MemoryStream();
                await jsonSerializer.SerializeAsync(progressStream, progress, encoding: CommonUtils.DefaultEncoding);
                return await ac.AnalyzeAsync(definitionStream, new EncodedStream(progressStream, CommonUtils.DefaultEncoding), inputPayloads, ct);
            },
            () => new AnalysisCoord(ambientService.NewUlid()),
            definitionStream,
            inputPayloads,
            agentPool,
            queuingPolicy,
            cancellationToken
        );
    }

    public async Task<QueuableAnalysisCoord> ReattemptAsync(
        Guid analysisId,
        GlobalMeta? globalMeta,
        EncodedStream? definitionStream,
        string? agentPool,
        QueuingPolicy queuingPolicy,
        CancellationToken cancellationToken
    )
    {
        if (await snapshotService.GetAnalysesAE(analysisId, true, cancellationToken).FirstOrDefaultAsync(cancellationToken) is not { } snapshot)
        {
            throw AnalysisExceptions.NoSuchAnalysis;
        }

        if (snapshot.FinishedAt is not null)
        {
            throw AnalysisExceptions.AlreadyPendingOrRunning;
        }

        GlobalMeta finalGlobalMeta = snapshot.GlobalMeta.WithOverwrite(globalMeta);
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos = await internalAnalysisService.CalculateStepsAsync(snapshot.Steps, cancellationToken);

        return await StartAsync(
            finalGlobalMeta,
            stepExecutorProtos,
            snapshot.Progress!,
            (ac, ct) => ac.ReattemptAsync(analysisId, definitionStream, ct),
            () => snapshot.AnalysisCoord with { Attempt = snapshot.Attempt + 1 },
            definitionStream,
            [ ],
            agentPool,
            queuingPolicy,
            cancellationToken
        );
    }

    private async Task<QueuableAnalysisCoord> StartAsync(
        GlobalMeta globalMeta,
        IEnumerable<AnalyzerStepExecutorProto2> stepExecutorProtos,
        JObject progress,
        Func<IAgentClient, CancellationToken, Task<ExtendedAnalysisCoord>> coreStartAsync,
        Func<AnalysisCoord> makeQueuedCoord,
        EncodedStream? definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        string? agentPool,
        QueuingPolicy queuingPolicy,
        CancellationToken cancellationToken
    )
    {
        agentPool ??= CoreOptions.DefaultAgentPool;

        Guid executionId;
        AnalysisCoord coord;
        bool queued;
        try
        {
            (executionId, object detail) = await executionService.StartAsync(
                agentPool,
                (lease, ct) => internalAnalysisService.HasConflictAsync(lease, stepExecutorProtos, ct),
                async (ac, ct) =>
                {
                    (Guid executionId0, Guid analysisId0, int attempt0) = await coreStartAsync(ac, ct);
                    return (executionId0, new AnalysisCoord(analysisId0, attempt0));
                },
                cancellationToken
            );

            coord = (AnalysisCoord)detail;
            queued = false;
        }
        catch (AnalysisException exception) when (ShouldQueue(exception))
        {
            coord = makeQueuedCoord();
            IEnumerable<StepInstance> steps = stepExecutorProtos.Select(static x => new StepInstance(x.Step.Meta, x.Input)).ToArray();
            executionId = await QueueAsync(agentPool, coord, globalMeta, steps, progress, definitionStream, inputPayloads, cancellationToken);
            queued = true;
        }

        bool ShouldQueue(AnalysisException ae)
        {
            return (ae.Label == IOrchestratorExecutionService.NoAgentAvailableExceptionLabel &&
                    (queuingPolicy & QueuingPolicy.IfFull) != 0) ||
                (ae.Label == nameof(AnalysisExceptions.ConflictingExecution) &&
                    (queuingPolicy & QueuingPolicy.IfConflict) != 0);
        }

        (Guid analysisId, int attempt) = coord;
        return new QueuableAnalysisCoord(executionId, analysisId, attempt, queued);
    }

    public async IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionsAE(Selection selection, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if ((selection & Selection.Running) != 0)
        {
            await foreach (ExtendedAnalysisCoord coord in CoreAbortRunningAE(null, cancellationToken))
            {
                yield return coord;
            }
        }

        if ((selection & Selection.Queued) != 0)
        {
            await foreach (AnalysisContextSnapshot snapshot in infoRepository.GetAllQueuedAnalysisSnapshotsAE(cancellationToken))
            {
                yield return await CoreAbortPendingAsync(snapshot);
            }
        }
    }

    public async IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionAE(Guid executionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (await snapshotService.GetAnalysisAsync(executionId, false, cancellationToken) is not { } snapshot)
        {
            yield break;
        }
        await foreach (ExtendedAnalysisCoord coord in CoreAbortAE(snapshot, cancellationToken))
        {
            yield return coord;
        }
    }

    public async IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysesAE(
        Guid analysisId, Selection selection, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        bool selectQueued = (selection & Selection.Queued) != 0;
        bool selectRunning = (selection & Selection.Running) != 0;

        await foreach (AnalysisContextSnapshot snapshot in snapshotService.GetAnalysesAE(analysisId, false, cancellationToken))
        {
            if (snapshot.StartedAt is null && selectQueued)
            {
                yield return await CoreAbortPendingAsync(snapshot);
            }
            else if (snapshot.FinishedAt is not null && selectRunning)
            {
                await foreach (ExtendedAnalysisCoord coord in CoreAbortRunningAE(snapshot.ExecutionId, cancellationToken))
                {
                    yield return coord;
                }
            }
        }
    }

    public async IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysisAE(AnalysisCoord coord, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (await snapshotService.GetAnalysisAsync(coord, false, cancellationToken) is not { } snapshot)
        {
            yield break;
        }
        await foreach (ExtendedAnalysisCoord coord0 in CoreAbortAE(snapshot, cancellationToken))
        {
            yield return coord0;
        }
    }

    private async IAsyncEnumerable<ExtendedAnalysisCoord> CoreAbortAE(
        AnalysisContextSnapshot snapshot, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        if (snapshot.StartedAt is null)
        {
            yield return await CoreAbortPendingAsync(snapshot);
        }
        else if (snapshot.FinishedAt is not null)
        {
            await foreach (ExtendedAnalysisCoord coord in CoreAbortRunningAE(snapshot.ExecutionId, cancellationToken))
            {
                yield return coord;
            }
        }
    }

    private async Task<ExtendedAnalysisCoord> CoreAbortPendingAsync(AnalysisContextSnapshot snapshot)
    {
        Guid executionId = snapshot.ExecutionId;
        (Guid analysisId, int attempt) = snapshot.AnalysisCoord;

        await infoRepository.DeleteAsync(executionId);
        await fileRepository.DeleteQueuedTreeAsync(executionId);

        return new ExtendedAnalysisCoord(executionId, analysisId, attempt);
    }

    private async IAsyncEnumerable<ExtendedAnalysisCoord> CoreAbortRunningAE(
        Guid? maybeExecutionId, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach ((Guid executionId, object detail) in executionService.AbortAE(ExecutionKind.Analysis, maybeExecutionId, cancellationToken))
        {
            (Guid analysisId, int attempt) = (AnalysisCoord)detail;
            yield return new ExtendedAnalysisCoord(executionId, analysisId, attempt);
        }
    }

    private async Task<Guid> QueueAsync(
        string agentPool,
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<StepInstance> steps,
        JObject progress,
        EncodedStream? definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        CancellationToken cancellationToken
    )
    {
        Guid executionId = ambientService.NewUlid();

        try
        {
            IAnalysisContext analysisContext = analysisContextFactory.Make(executionId, coord, globalMeta, steps, progress, agentPool);
            await infoRepository.InsertAsync(analysisContext);

            if (definitionStream is not null)
            {
                await fileRepository.WriteQueuedDefinitionAsync(definitionStream, executionId, cancellationToken);
            }

            int parallelism = globalMeta.Parallelism ?? CoreOptions.DefaultParallelism;
            await Parallel.ForEachAsync(
                inputPayloads,
                new ParallelOptions() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = parallelism },
                async (inputPayload, _) =>
                {
                    await fileRepository.WriteQueuedInputPayloadAsync(inputPayload, executionId, inputPayload.Label, cancellationToken);
                }
            );
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        {
            await fileRepository.DeleteQueuedTreeAsync(executionId);
            await infoRepository.DeleteAsync(executionId);
            throw;
        }

        return executionId;
    }
}
