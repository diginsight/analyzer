using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AgentExecutionService : IAgentExecutionService
{
    private static readonly AnalysisException ShuttingDownException =
        new ("Shutting down", HttpStatusCode.ServiceUnavailable, "ShuttingDown");

    private readonly ILogger logger;
    private readonly IAmbientService ambientService;
    private readonly IAgentLeaseService leaseService;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly SemaphoreSlim semaphore = new (1, 1);

    private bool isShuttingDown = false;
    private (CancellationTokenSource CancellationSource, ExecutionCoord Coord, object Detail, ManualResetEventSlim Mre)? current;

    public AgentExecutionService(
        ILogger<AgentExecutionService> logger,
        IAmbientService ambientService,
        IAgentLeaseService leaseService,
        IHostApplicationLifetime applicationLifetime,
        IServiceScopeFactory serviceScopeFactory
    )
    {
        this.logger = logger;
        this.ambientService = ambientService;
        this.leaseService = leaseService;
        this.applicationLifetime = applicationLifetime;
        this.serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<Guid> StartAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedExecutionId,
        object detail,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<Guid, CancellationToken, Task> coreStartAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        Guid executionId = await LockExecutionAsync(kind, requestedExecutionId, detail, fillLease, hasConflictAsync, cancellationToken);

        LogMessages.ExecutionStarted(logger, kind, executionId);

        bool faulted = true;
        bool cancelled = false;
        try
        {
            await coreStartAsync(executionId, cancellationToken);
            faulted = false;

            return executionId;
        }
        catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
        {
            cancelled = true;
            throw;
        }
        finally
        {
            if (faulted)
            {
                await UnlockExecutionAsync(cancelled);
            }
        }
    }

    public async IAsyncEnumerable<(Guid Id, object Detail)> AbortAE(
        ExecutionKind kind, Guid? executionId, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await semaphore.WaitAsync(CancellationToken.None);
        try
        {
            if (current is var (cancellationSource, (kind0, executionId0), detail, _) && kind0 == kind && executionId0 == (executionId ?? executionId0))
            {
                await cancellationSource.CancelAsync();
                yield return (executionId0, detail);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task RunDetachedAsync<TServices>(
        Func<IServiceProvider, ICollection<IDisposable>, TServices> getServices,
        Func<TServices, CancellationToken, Task> runAsync
    )
    {
        TaskCompletionSource tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(
            async () =>
            {
                using IServiceScope serviceScope = serviceScopeFactory.CreateScope();
                IServiceProvider scopeServiceProvider = serviceScope.ServiceProvider;

                ICollection<IDisposable> disposables = new List<IDisposable>();
                try
                {
                    TServices services;
                    try
                    {
                        services = getServices(scopeServiceProvider, disposables);
                    }
                    catch (Exception exception)
                    {
                        tcs.SetException(exception);
                        return;
                    }

                    tcs.SetResult();

                    bool cancelled = false;
                    CancellationToken cancellationToken = current!.Value.CancellationSource.Token;
                    try
                    {
                        await runAsync(services, cancellationToken);
                    }
                    catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
                    {
                        cancelled = true;
                    }
                    catch (Exception exception)
                    {
                        LogMessages.UnexpectedErrorDuringExecution(logger, exception);
                    }
                    finally
                    {
                        await UnlockExecutionAsync(cancelled);
                    }
                }
                finally
                {
                    foreach (IDisposable disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        );

        return tcs.Task;
    }

    public async Task WaitForFinishAsync()
    {
        ManualResetEventSlim? mre;

        await semaphore.WaitAsync();
        try
        {
            isShuttingDown = true;
            mre = current?.Mre;
        }
        finally
        {
            semaphore.Release();
        }

        mre?.Wait();
    }

    public async Task<(ExecutionCoord Coord, object Detail)?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(CancellationToken.None);
        try
        {
            return current is var (_, executionCoord, detail, _) ? (executionCoord, detail) : null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<Guid> LockExecutionAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedExecutionId,
        object detail,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        LogMessages.LockingExecution(logger);

        await semaphore.WaitAsync(CancellationToken.None);
        try
        {
            if (isShuttingDown)
            {
                LogMessages.ShuttingDown(logger);
                throw ShuttingDownException;
            }

            if (current is var (_, (otherKind, otherExecutionId), _, _))
            {
                LogMessages.AlreadyExecuting(logger, otherKind, otherExecutionId);
                throw AnalysisExceptions.AlreadyExecuting(otherKind, otherExecutionId);
            }

            Guid executionId = requestedExecutionId ?? ambientService.NewUlid();

            await leaseService.AcquireExecutionAsync(executionId, fillLease, hasConflictAsync, cancellationToken);

            current = (
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, applicationLifetime.ApplicationStopping),
                new ExecutionCoord(kind, executionId),
                detail,
                new ManualResetEventSlim()
            );

            return executionId;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task UnlockExecutionAsync(bool cancelled)
    {
        await semaphore.WaitAsync();
        try
        {
            await leaseService.ReleaseExecutionAsync();

            using (ManualResetEventSlim mre = current!.Value.Mre)
            {
                mre.Set();
            }

            if (cancelled)
            {
                LogMessages.ExecutionCancelled(logger);
            }
            else
            {
                LogMessages.ExecutionFinished(logger);
            }

            current = null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Locking execution")]
        internal static partial void LockingExecution(ILogger logger);

        [LoggerMessage(1, LogLevel.Warning, "Already executing {Kind} '{ExecutionId}'")]
        internal static partial void AlreadyExecuting(ILogger logger, ExecutionKind kind, Guid executionId);

        [LoggerMessage(2, LogLevel.Information, "Execution {Kind} started with id '{ExecutionId}'")]
        internal static partial void ExecutionStarted(ILogger logger, ExecutionKind kind, Guid executionId);

        [LoggerMessage(3, LogLevel.Information, "Execution finished")]
        internal static partial void ExecutionFinished(ILogger logger);

        [LoggerMessage(4, LogLevel.Information, "Execution cancelled")]
        internal static partial void ExecutionCancelled(ILogger logger);

        [LoggerMessage(5, LogLevel.Error, "Unexpected error during execution")]
        internal static partial void UnexpectedErrorDuringExecution(ILogger logger, Exception exception);

        [LoggerMessage(6, LogLevel.Information, "Shutting down")]
        internal static partial void ShuttingDown(ILogger logger);

        [LoggerMessage(7, LogLevel.Warning, "Duplicate execution id")]
        internal static partial void DuplicateExecutionId(ILogger logger);
    }
}
