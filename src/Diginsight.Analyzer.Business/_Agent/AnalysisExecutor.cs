using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities.Events;
using Diginsight.Analyzer.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading.RateLimiting;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AnalysisExecutor : IAnalysisExecutor
{
    private readonly ILogger logger;
    private readonly TimeProvider timeProvider;
    private readonly IAgentAnalysisContextFactory analysisContextFactory;
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly IEventService eventService;
    private readonly ICoreOptions coreOptions;

    public AnalysisExecutor(
        ILogger<AnalysisExecutor> logger,
        TimeProvider timeProvider,
        IAgentAnalysisContextFactory analysisContextFactory,
        IAnalysisInfoRepository infoRepository,
        IEventService eventService,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.logger = logger;
        this.timeProvider = timeProvider;
        this.analysisContextFactory = analysisContextFactory;
        this.infoRepository = infoRepository;
        this.eventService = eventService;
        this.coreOptions = coreOptions.Value;
    }

    public async Task ExecuteAsync(
        Guid executionId,
        AnalysisCoord coord,
        DateTime? queuedAt,
        GlobalMeta globalMeta,
        IEnumerable<IAnalyzerStepExecutor> stepExecutors,
        IEnumerable<IEventSender> eventSenders,
        JObject progress,
        CancellationToken cancellationToken
    )
    {
        IAgentAnalysisContext analysisContext = analysisContextFactory.Make(
            executionId,
            coord,
            globalMeta,
            stepExecutors.Select(static x => new StepInstance(x.Meta, x.RawInput)),
            progress,
            queuedAt
        );

        DateTime startedAt = analysisContext.StartedAt;
        JObject eventMeta = globalMeta.EventMeta ?? new JObject();
        ExecutionCoord executionCoord = new (ExecutionKind.Analysis, executionId);

        await infoRepository.InsertAsync(analysisContext);
        await eventService.EmitAsync(
            eventSenders,
            _ => new AnalysisStartedEvent()
            {
                ExecutionCoord = executionCoord,
                AnalysisCoord = coord,
                Timestamp = startedAt,
                Meta = eventMeta,
                Queued = queuedAt is not null,
            }
        );

        try
        {
            await WithTimeBoundAsync(CoreExecuteAsync, analysisContext, analysisContext, cancellationToken);
            SetOverallStatus(analysisContext);
        }
        catch (Exception exception) when (exception is not OperationCanceledException oce || oce.CancellationToken != cancellationToken)
        {
            analysisContext.Fail(exception);
        }
        finally
        {
            await infoRepository.UpsertAsync(analysisContext);
            await eventService.EmitAsync(
                eventSenders,
                _ => new AnalysisFinishedEvent()
                {
                    ExecutionCoord = executionCoord,
                    AnalysisCoord = coord,
                    Timestamp = analysisContext.FinishedAt!.Value,
                    Meta = eventMeta,
                    Status = analysisContext.IsFailed ? FinishedEventStatus.Failed
                        : analysisContext.Status == TimeBoundStatus.Aborted ? FinishedEventStatus.Aborted
                        : FinishedEventStatus.Completed,
                }
            );
        }

        async Task CoreExecuteAsync(CancellationToken ct)
        {
            int parallelism = globalMeta.Parallelism ?? coreOptions.DefaultParallelism;

            Task ExecuteSideAsync(bool isSetup) =>
                Parallel.ForEachAsync(
                    isSetup ? stepExecutors : stepExecutors.Reverse(),
                    new ParallelOptions() { CancellationToken = ct, MaxDegreeOfParallelism = parallelism },
                    async (stepExecutor, ct0) =>
                    {
                        if (!analysisContext.IsSucceeded())
                            return;

                        if (isSetup)
                            LogMessages.RunningAnalyzerStepSetup(logger, stepExecutor.Meta.InternalName);
                        else
                            LogMessages.RunningAnalyzerStepTeardown(logger, stepExecutor.Meta.InternalName);

                        await WithStepHistoryAsync(
                            isSetup
                                ? (stepHistory, ct1) => stepExecutor.SetupAsync(analysisContext, stepHistory, ct1)
                                : (stepHistory, ct1) => stepExecutor.TeardownAsync(analysisContext, stepHistory, ct1),
                            analysisContext,
                            stepExecutor,
                            eventSenders,
                            ct0
                        );
                    }
                );

            await ExecuteSideAsync(true);

            await ExecuteMainAsync(stepExecutors, eventSenders, analysisContext, parallelism, ct);

            await ExecuteSideAsync(false);
        }
    }

    private static void SetOverallStatus(IAnalysisContext analysisContext)
    {
        IDictionary<string, IEnumerable<IEnumerable<string>>> trailsByStep = new Dictionary<string, IEnumerable<IEnumerable<string>>>();

        IEnumerable<IEnumerable<string>> GetTrails(string step)
        {
            if (trailsByStep.TryGetValue(step, out IEnumerable<IEnumerable<string>>? trails))
            {
                return trails;
            }

            IEnumerable<string> parentSteps = analysisContext.GetStep(step).Meta.DependsOn;
            if (!parentSteps.Any())
            {
                return trailsByStep[step] = [ [ step ] ];
            }

            return trailsByStep[step] = parentSteps
                .SelectMany(GetTrails)
                .Select(ts => ts.Prepend(step).ToArray())
                .ToArray();
        }

        IEnumerable<IEnumerable<string>> trails = analysisContext.Steps.Select(static x => x.Meta.InternalName)
            .Except(analysisContext.Steps.SelectMany(static x => x.Meta.DependsOn))
            .SelectMany(GetTrails);

        foreach (IEnumerable<string> trail in trails)
        {
            foreach (string step in trail)
            {
                IStepHistoryRO stepHistory = analysisContext.GetStep(step);

                if (stepHistory.IsSkipped)
                {
                    continue;
                }
                if (stepHistory.IsFailed)
                {
                    analysisContext.Fail("StepFailure");
                    return;
                }

                switch (stepHistory.Status)
                {
                    case TimeBoundStatus.Pending:
                    case TimeBoundStatus.Running:
                        throw new UnreachableException($"Unexpected {nameof(TimeBoundStatus)}");

                    case TimeBoundStatus.Completed:
                    case TimeBoundStatus.PartiallyCompleted:
                    case TimeBoundStatus.Aborting:
                    case TimeBoundStatus.Aborted:
                        goto nextTrail;

                    default:
                        throw new UnreachableException($"Unrecognized {nameof(TimeBoundStatus)}");
                }
            }

            nextTrail: ;
        }
    }

    private async Task ExecuteMainAsync(
        IEnumerable<IAnalyzerStepExecutor> stepExecutors,
        IEnumerable<IEventSender> eventSenders,
        IAgentAnalysisContext analysisContext,
        int parallelism,
        CancellationToken cancellationToken
    )
    {
        await using RateLimiter rateLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions() { PermitLimit = parallelism, QueueLimit = int.MaxValue });

        TaskCompletionSource tcs = new ();

        Lock @lock = new ();
        IList<IAnalyzerStepExecutor> missingStepExecutors = new List<IAnalyzerStepExecutor>(stepExecutors);
        int totalStepCount = missingStepExecutors.Count;
        ISet<string> completedStepNames = new HashSet<string>();

        CancellationTokenSource localCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken localCancellationToken = localCancellationTokenSource.Token;

        Enqueue();

        try
        {
            await tcs.Task;
        }
        finally
        {
            await localCancellationTokenSource.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        void Enqueue()
        {
            if (!analysisContext.IsSucceeded())
            {
                tcs.TrySetResult();
                return;
            }

            IReadOnlyList<IAnalyzerStepExecutor> localStepExecutors;
            lock (@lock)
            {
                if (localCancellationToken.IsCancellationRequested || completedStepNames.Count == totalStepCount)
                {
                    tcs.TrySetResult();
                    return;
                }

                localStepExecutors = missingStepExecutors.ToArray();
            }

            foreach (IAnalyzerStepExecutor stepExecutor in localStepExecutors)
            {
                StepMeta stepMeta = stepExecutor.Meta;

                lock (@lock)
                {
                    if (!completedStepNames.IsSupersetOf(stepMeta.DependsOn))
                        continue;

                    if (!missingStepExecutors.Remove(stepExecutor))
                        continue;
                }

                TaskUtils.RunAndForget(
                    async () =>
                    {
                        (_, string internalName) = stepMeta;

                        RateLimitLease lease;
                        try
                        {
                            lease = await rateLimiter.AcquireAsync(cancellationToken: localCancellationToken);
                        }
                        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
                        {
                            tcs.TrySetCanceled(cancellationToken);
                            return;
                        }
                        catch (OperationCanceledException exception) when (exception.CancellationToken == localCancellationToken)
                        {
                            tcs.TrySetResult();
                            return;
                        }
                        catch (Exception exception)
                        {
                            LogMessages.ErrorRunningAnalyzerStep(logger, internalName, exception);
                            tcs.TrySetException(exception);
                            return;
                        }

                        using (lease)
                        {
                            if (!lease.IsAcquired)
                            {
                                tcs.TrySetException(new InvalidOperationException("Could not acquire lease from rate limiter"));
                                return;
                            }
                            try
                            {
                                LogMessages.RunningAnalyzerStep(logger, internalName);

                                await WithStepHistoryAsync(
                                    ExecuteIfEnabledAsync,
                                    analysisContext,
                                    stepExecutor,
                                    eventSenders,
                                    localCancellationToken
                                );

                                lock (@lock)
                                {
                                    completedStepNames.Add(stepMeta.InternalName);
                                }

                                Enqueue();

                                async Task ExecuteIfEnabledAsync(IStepHistory stepHistory, CancellationToken ct)
                                {
                                    if (!stepExecutor.Condition.TryEvaluate(analysisContext, stepHistory, out bool enabled))
                                    {
                                        return;
                                    }

                                    if (enabled)
                                    {
                                        await stepExecutor.ExecuteAsync(analysisContext, stepHistory, ct);
                                    }
                                    else
                                    {
                                        stepHistory.Skip("Condition");
                                    }
                                }
                            }
                            catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
                            {
                                tcs.TrySetCanceled(cancellationToken);
                            }
                            catch (OperationCanceledException exception) when (exception.CancellationToken == localCancellationToken)
                            {
                                tcs.TrySetResult();
                            }
                            catch (Exception exception)
                            {
                                LogMessages.ErrorRunningAnalyzerStep(logger, internalName, exception);
                                tcs.TrySetException(exception);
                            }
                        }
                    },
                    localCancellationToken
                );
            }
        }
    }

    private async Task WithTimeBoundAsync(
        Func<CancellationToken, Task> runAsync, ITimeBound timeBound, IAnalysisContext analysisContext, CancellationToken cancellationToken
    )
    {
        TimeBoundStatus completedStatus = timeBound is ITimeBoundWithPhases { FinishedAt: null }
            ? TimeBoundStatus.PartiallyCompleted
            : TimeBoundStatus.Completed;

        await using CancellationTokenRegistration registration = cancellationToken.Register(
            () =>
            {
                timeBound.Status = TimeBoundStatus.Aborting;
                infoRepository.UpsertAsync(analysisContext).GetAwaiter().GetResult();
            }
        );

        try
        {
            await runAsync(cancellationToken);

            registration.Unregister();
            timeBound.Status = completedStatus;
        }
        catch (Exception exception)
        {
            registration.Unregister();
            timeBound.Status = exception is OperationCanceledException oce && oce.CancellationToken == cancellationToken
                ? TimeBoundStatus.Aborted : completedStatus;
            throw;
        }
        finally
        {
            DateTime finishedAt = timeProvider.GetUtcNow().UtcDateTime;
            if (timeBound is not ITimeBoundWithPhases timeBoundWithPhases)
            {
                timeBound.FinishedAt = finishedAt;
            }
            else if (timeBoundWithPhases is { SetupFinishedAt: null })
            {
                timeBoundWithPhases.SetupFinishedAt = finishedAt;
            }
            else if (timeBoundWithPhases is { FinishedAt: null })
            {
                timeBoundWithPhases.FinishedAt = finishedAt;
            }
            else
            {
                timeBoundWithPhases.TeardownFinishedAt = finishedAt;
            }
        }
    }

    private async Task WithStepHistoryAsync(
        Func<IStepHistory, CancellationToken, Task> runAsync,
        IAgentAnalysisContext analysisContext,
        IAnalyzerStepExecutor stepExecutor,
        IEnumerable<IEventSender> eventSenders,
        CancellationToken cancellationToken
    )
    {
        ExecutionCoord executionCoord = analysisContext.ExecutionCoord;
        AnalysisCoord analysisCoord = analysisContext.AnalysisCoord;
        JObject eventMeta = analysisContext.GlobalMeta.EventMeta ?? new JObject();
        (string template, string internalName) = stepExecutor.Meta;

        StepHistory stepHistory = analysisContext.GetStep(internalName);
        stepHistory.Status = TimeBoundStatus.Running;

        DateTime startedAt = timeProvider.GetUtcNow().UtcDateTime;
        Phase phase;
        if (stepHistory.SetupStartedAt is null)
        {
            phase = Phase.Setup;
            stepHistory.SetupStartedAt = startedAt;
        }
        else if (stepHistory.StartedAt is null)
        {
            phase = Phase.Process;
            stepHistory.StartedAt = startedAt;
        }
        else
        {
            phase = Phase.Teardown;
            stepHistory.TeardownStartedAt = startedAt;
        }

        await infoRepository.UpsertAsync(analysisContext);
        await eventService.EmitAsync(
            eventSenders,
            _ => new StepStartedEvent()
            {
                ExecutionCoord = executionCoord,
                AnalysisCoord = analysisCoord,
                Timestamp = startedAt,
                Meta = eventMeta,
                Template = template,
                InternalName = internalName,
                Phase = phase,
            }
        );

        try
        {
            using IDisposable? timer = stepExecutor.DisableProgressFlushTimer
                ? null
                : infoRepository.StartTimedProgressFlush(analysisContext);
            await WithTimeBoundAsync(ct => runAsync(stepHistory, ct), stepHistory, analysisContext, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException oce || oce.CancellationToken != cancellationToken)
        {
            stepHistory.Fail(exception);
        }
        finally
        {
            await infoRepository.UpsertAsync(analysisContext);
            await eventService.EmitAsync(
                eventSenders,
                _ => new StepFinishedEvent()
                {
                    ExecutionCoord = executionCoord,
                    AnalysisCoord = analysisCoord,
                    Timestamp = phase switch
                    {
                        Phase.Setup => stepHistory.SetupFinishedAt!.Value,
                        Phase.Process => stepHistory.FinishedAt!.Value,
                        Phase.Teardown => stepHistory.TeardownFinishedAt!.Value,
                        _ => throw new UnreachableException($"Unrecognized {nameof(Phase)}"),
                    },
                    Meta = eventMeta,
                    Template = template,
                    InternalName = internalName,
                    Phase = phase,
                    Status = !stepHistory.IsFailed ? FinishedEventStatus.Failed
                        : stepHistory.IsSkipped ? FinishedEventStatus.Skipped
                        : stepHistory.Status == TimeBoundStatus.Aborted ? FinishedEventStatus.Aborted
                        : FinishedEventStatus.Completed,
                }
            );
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Information, "Running analyzer step '{InternalName}', setup phase")]
        internal static partial void RunningAnalyzerStepSetup(ILogger logger, string internalName);

        [LoggerMessage(1, LogLevel.Information, "Running analyzer step '{InternalName}'")]
        internal static partial void RunningAnalyzerStep(ILogger logger, string internalName);

        [LoggerMessage(2, LogLevel.Error, "Error running analyzer step '{InternalName}'")]
        internal static partial void ErrorRunningAnalyzerStep(ILogger logger, string internalName, Exception exception);

        [LoggerMessage(3, LogLevel.Information, "Running analyzer step '{InternalName}', teardown phase")]
        internal static partial void RunningAnalyzerStepTeardown(ILogger logger, string internalName);
    }
}
