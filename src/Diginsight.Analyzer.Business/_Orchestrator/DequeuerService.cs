using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed partial class DequeuerService : BackgroundService, IDequeuerService
{
    private readonly ILogger logger;
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly IOrchestratorExecutionService executionService;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IOptionsMonitor<CoreOptions> coreOptionsMonitor;

    private CancellationTokenSource? loopCancellationTokenSource;

    private IOrchestratorCoreOptions CoreOptions => coreOptionsMonitor.CurrentValue;

    public DequeuerService(
        ILogger<DequeuerService> logger,
        IAnalysisInfoRepository infoRepository,
        IOrchestratorExecutionService executionService,
        IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<CoreOptions> coreOptionsMonitor
    )
    {
        this.logger = logger;
        this.infoRepository = infoRepository;
        this.executionService = executionService;
        this.serviceScopeFactory = serviceScopeFactory;
        this.coreOptionsMonitor = coreOptionsMonitor;
    }

    public void TriggerDequeue()
    {
        loopCancellationTokenSource?.Cancel();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LogMessages.LookingForQueuedAnalyses(logger);

            int failures = 0;

            try
            {
                IAsyncEnumerable<AnalysisContextSnapshot> snapshots = infoRepository.GetAllQueuedAnalysisSnapshotsAE(cancellationToken);
                await foreach (AnalysisContextSnapshot snapshot in snapshots)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool proceed;
                    Guid executionId = snapshot.ExecutionId;

                    LogMessages.QueuedAnalysisFound(logger, executionId);

                    using IServiceScope serviceScope = serviceScopeFactory.CreateScope();

                    try
                    {
                        proceed = await executionService.DequeueAsync(executionId, snapshot.AgentPool, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        if (exception is not AnalysisException { Label: nameof(AnalysisExceptions.ConflictingExecution) })
                        {
                            LogMessages.ErrorInvokingAgent(logger, exception);
                        }

                        failures += 1;
                        proceed = failures < CoreOptions.DequeuerMaxFailures;
                    }

                    if (!proceed)
                    {
                        LogMessages.SuspendingLoop(logger);
                        break;
                    }
                }
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken) { }
            catch (Exception exception)
            {
                LogMessages.UnexpectedErrorDuringDequeue(logger, exception);
            }

            loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(CoreOptions.DequeuerIntervalSeconds), loopCancellationTokenSource.Token);
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken != cancellationToken) { }
            finally
            {
                loopCancellationTokenSource = null;
            }
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Warning, "Error invoking agent")]
        internal static partial void ErrorInvokingAgent(ILogger logger, Exception exception);

        [LoggerMessage(1, LogLevel.Error, "Unexpected error during dequeue")]
        internal static partial void UnexpectedErrorDuringDequeue(ILogger logger, Exception exception);

        [LoggerMessage(2, LogLevel.Debug, "Looking for queued analyses")]
        internal static partial void LookingForQueuedAnalyses(ILogger logger);

        [LoggerMessage(3, LogLevel.Information, "Queued analysis found with execution '{ExecutionId}'")]
        internal static partial void QueuedAnalysisFound(ILogger logger, Guid executionId);

        [LoggerMessage(4, LogLevel.Debug, "Suspending queued lookup loop")]
        internal static partial void SuspendingLoop(ILogger logger);
    }
}
