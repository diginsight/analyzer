using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities.Events;
using Diginsight.Analyzer.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AnalysisExecutor : IAnalysisExecutor
{
    private readonly ILogger logger;
    private readonly TimeProvider timeProvider;
    private readonly IAgentAnalysisContextFactory analysisContextFactory;
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly IEventService eventService;
    private readonly IEvaluatorFactory evaluatorFactory;
    private readonly ICoreOptions coreOptions;

    public AnalysisExecutor(
        ILogger<AnalysisExecutor> logger,
        TimeProvider timeProvider,
        IAgentAnalysisContextFactory analysisContextFactory,
        IAnalysisInfoRepository infoRepository,
        IEventService eventService,
        IEvaluatorFactory evaluatorFactory,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.logger = logger;
        this.timeProvider = timeProvider;
        this.analysisContextFactory = analysisContextFactory;
        this.infoRepository = infoRepository;
        this.eventService = eventService;
        this.evaluatorFactory = evaluatorFactory;
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
        IAnalysisContext analysisContext = analysisContextFactory.Make(
            executionId,
            coord,
            globalMeta,
            stepExecutors.Select(static x => new StepInstance(x.Meta, x.Input)),
            progress,
            queuedAt
        );

        DateTime startedAt = analysisContext.StartedAt;
        IEnumerable<EventRecipient> eventRecipients = globalMeta.EventRecipients ?? [ ];
        ExecutionCoord executionCoord = new(ExecutionKind.Analysis, executionId);
        IEvaluator evaluator = evaluatorFactory.Make(analysisContext);

        await infoRepository.InsertAsync(analysisContext);
        await eventService.EmitAsync(
            eventSenders,
            eventRecipients,
            (r, _) => new AnalysisStartedEvent()
            {
                ExecutionCoord = executionCoord,
                AnalysisCoord = coord,
                Timestamp = startedAt,
                RecipientInput = r,
                Queued = queuedAt is not null,
            }
        );

        try
        {
            await WithTimeBoundAsync(CoreExecuteAsync, analysisContext, analysisContext, cancellationToken);
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
                eventRecipients,
                (r, _) => new AnalysisFinishedEvent()
                {
                    ExecutionCoord = executionCoord,
                    AnalysisCoord = coord,
                    Timestamp = analysisContext.FinishedAt!.Value,
                    RecipientInput = r,
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
                                ? ct1 => stepExecutor.SetupAsync(analysisContext, ct1)
                                : ct1 => stepExecutor.TeardownAsync(analysisContext, ct1),
                            analysisContext,
                            stepExecutor,
                            eventSenders,
                            ct0
                        );
                    }
                );

            await ExecuteSideAsync(true);

            await ExecuteMainAsync(stepExecutors, eventSenders, analysisContext, evaluator, parallelism, ct);

            await ExecuteSideAsync(false);
        }
    }

    private Task ExecuteMainAsync(
        IEnumerable<IAnalyzerStepExecutor> stepExecutors,
        IEnumerable<IEventSender> eventSenders,
        IAnalysisContext analysisContext,
        IEvaluator evaluator,
        int parallelism,
        CancellationToken cancellationToken
    )
    {
        TaskScheduler taskScheduler = new LimitedConcurrencyTaskScheduler(parallelism);
        TaskFactory taskFactory = new (taskScheduler);
        TaskCompletionSource tcs = new ();

        Lock @lock = new ();
        IList<IAnalyzerStepExecutor> missingStepExecutors = new List<IAnalyzerStepExecutor>(stepExecutors);
        ISet<string> completedStepNames = new HashSet<string>();

        Enqueue();

        return tcs.Task;

        void Enqueue()
        {
            if (!analysisContext.IsSucceeded())
            {
                tcs.TrySetResult();
                return;
            }

            IAnalyzerStepExecutor[] localStepExecutors;
            lock (@lock)
            {
                localStepExecutors = missingStepExecutors.ToArray();
            }

            if (!(localStepExecutors.Length > 0))
            {
                tcs.TrySetResult();
                return;
            }

            foreach (IAnalyzerStepExecutor stepExecutor in localStepExecutors)
            {
                lock (@lock)
                {
                    if (!completedStepNames.IsSupersetOf(stepExecutor.Meta.DependsOn))
                        continue;

                    if (!missingStepExecutors.Remove(stepExecutor))
                        continue;
                }

                _ = taskFactory
                    .StartNew(
                        async () =>
                        {
                            (_, string internalName) = stepExecutor.Meta;

                            try
                            {
                                LogMessages.RunningAnalyzerStep(logger, internalName);

                                await WithStepHistoryAsync(
                                    ExecuteIfEnabledAsync,
                                    analysisContext,
                                    stepExecutor,
                                    eventSenders,
                                    cancellationToken
                                );

                                lock (@lock)
                                {
                                    completedStepNames.Add(stepExecutor.Meta.InternalName);
                                }

                                Enqueue();

                                async Task ExecuteIfEnabledAsync(CancellationToken ct)
                                {
                                    StepHistory stepHistory = analysisContext.GetStep(internalName);
                                    if (!evaluator.TryEvalCondition(stepHistory, out bool enabled))
                                    {
                                        return;
                                    }

                                    if (enabled)
                                    {
                                        await stepExecutor.ExecuteAsync(analysisContext, ct);
                                    }
                                    else
                                    {
                                        stepHistory.Skip("Condition");
                                    }
                                }
                            }
                            catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken) { }
                            catch (Exception exception)
                            {
                                LogMessages.ErrorRunningAnalyzerStep(logger, internalName, exception);
                                tcs.TrySetException(exception);
                            }
                        },
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )
                    .Unwrap();
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
        Func<CancellationToken, Task> runAsync,
        IAnalysisContext analysisContext,
        IAnalyzerStepExecutor stepExecutor,
        IEnumerable<IEventSender> eventSenders,
        CancellationToken cancellationToken
    )
    {
        ExecutionCoord executionCoord = analysisContext.ExecutionCoord;
        AnalysisCoord analysisCoord = analysisContext.AnalysisCoord;
        IEnumerable<EventRecipient> eventRecipients = analysisContext.GlobalMeta.EventRecipients ?? [ ];
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
            eventRecipients,
            (r, _) => new StepStartedEvent()
            {
                ExecutionCoord = executionCoord,
                AnalysisCoord = analysisCoord,
                Timestamp = startedAt,
                RecipientInput = r,
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
            await WithTimeBoundAsync(runAsync, stepHistory, analysisContext, cancellationToken);
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
                eventRecipients,
                (r, _) => new StepFinishedEvent()
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
                    RecipientInput = r,
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
