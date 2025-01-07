namespace Diginsight.Analyzer.Business;

internal interface IAgentLeaseService
{
    Task CreateAsync();

    Task AcquireExecutionAsync<TLease>(
        Guid executionId,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new();

    Task ReleaseExecutionAsync();

    Task DeleteAsync();
}
