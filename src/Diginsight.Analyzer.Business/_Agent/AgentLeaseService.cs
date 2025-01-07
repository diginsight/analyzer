using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AgentLeaseService : IAgentLeaseService
{
    private readonly ILogger logger;
    private readonly IAgentAmbientService ambientService;
    private readonly ILeaseRepository leaseRepository;
    private readonly IAgentCoreOptions coreOptions;
    private readonly SemaphoreSlim semaphore = new (1, 1);

    private Lease? lease;
    private IDisposable? keepaliveDisposable;

    public AgentLeaseService(
        ILogger<AgentLeaseService> logger,
        IAgentAmbientService ambientService,
        ILeaseRepository leaseRepository,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.logger = logger;
        this.ambientService = ambientService;
        this.leaseRepository = leaseRepository;
        this.coreOptions = coreOptions.Value;
    }

    public Task CreateAsync()
    {
        int ttlMinutes = Math.Max(coreOptions.LeaseTtlMinutes, 1);

        return WithSemaphore(
            async () =>
            {
                LogMessages.CreatingLease(logger);

                lease = new Lease()
                {
                    Id = ambientService.NewUlid().ToString("D"),
                    BaseAddress = ambientService.BaseAddress,
                    AgentName = ambientService.AgentName,
                    AgentPool = ambientService.AgentPool,
                    TtlSeconds = (int)TimeSpan.FromMinutes(ttlMinutes).TotalSeconds,
                };
                await leaseRepository.UpsertAsync(lease);

                Timer timer = new (TimeSpan.FromMinutes(ttlMinutes - 0.5).TotalMilliseconds) { AutoReset = true };

                async Task KeepaliveAsync()
                {
                    await WithSemaphore(
                        async () =>
                        {
                            try
                            {
                                if (lease is null)
                                {
                                    return;
                                }

                                await leaseRepository.UpsertAsync(lease);
                            }
                            catch (Exception e)
                            {
                                _ = e;
                            }
                        }
                    );
                }

                timer.Elapsed += (_, _) => { KeepaliveAsync().GetAwaiter().GetResult(); };
                timer.Start();

                keepaliveDisposable = timer;
            }
        );
    }

    public Task AcquireExecutionAsync<TLease>(
        Guid executionId,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        return WithSemaphore(
            async () =>
            {
                LogMessages.AcquiringTemporaryExecution(logger);

                TLease newLease = lease!.As<TLease>();
                newLease.ExecutionId = executionId;
                fillLease(newLease);

                await leaseRepository.UpsertAsync(newLease);

                await foreach (ActiveLease otherLease in leaseRepository
                    .GetActiveExceptAE(lease.Id, cancellationToken)
                    .Select(static x => x.AsActive()!)
                    .WithCancellation(cancellationToken))
                {
                    if (!await hasConflictAsync(otherLease, cancellationToken))
                    {
                        continue;
                    }

                    ExecutionKind otherKind = otherLease.Kind!.Value;
                    Guid otherExecutionId = otherLease.ExecutionId;

                    LogMessages.ConflictingExecution(logger, otherKind, otherExecutionId);

                    await leaseRepository.UpsertAsync(lease);

                    throw AnalysisExceptions.ConflictingExecution(otherKind, otherExecutionId);
                }

                LogMessages.ExecutionConfirmed(logger);

                lease = newLease;
            }
        );
    }

    public Task ReleaseExecutionAsync()
    {
        return WithSemaphore(
            () =>
            {
                LogMessages.ReleasingExecution(logger);

                lease = new Lease(lease!);

                return leaseRepository.UpsertAsync(lease);
            }
        );
    }

    public Task DeleteAsync()
    {
        return WithSemaphore(
            async () =>
            {
                LogMessages.DeletingLease(logger);

                if (keepaliveDisposable is not null)
                {
                    keepaliveDisposable.Dispose();
                    keepaliveDisposable = null;
                }

                if (lease is not null)
                {
                    await leaseRepository.DeleteAsync(lease);
                    lease = null;
                }
            }
        );
    }

    // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
    private async Task WithSemaphore(Func<Task> runAsync)
    {
        await semaphore.WaitAsync();
        try
        {
            await runAsync();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Creating lease")]
        internal static partial void CreatingLease(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Acquiring temporary execution")]
        internal static partial void AcquiringTemporaryExecution(ILogger logger);

        [LoggerMessage(2, LogLevel.Warning, "Found conflicting {Kind} execution with id {ExecutionId}")]
        internal static partial void ConflictingExecution(ILogger logger, ExecutionKind kind, Guid executionId);

        [LoggerMessage(3, LogLevel.Debug, "Execution confirmed")]
        internal static partial void ExecutionConfirmed(ILogger logger);

        [LoggerMessage(4, LogLevel.Debug, "Releasing execution")]
        internal static partial void ReleasingExecution(ILogger logger);

        [LoggerMessage(5, LogLevel.Debug, "Deleting lease")]
        internal static partial void DeletingLease(ILogger logger);
    }
}
