namespace Diginsight.Analyzer.Business;

internal interface IAgentExecutionService : IExecutionService
{
    Task<Guid> StartAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedExecutionId,
        object detail,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<Guid, CancellationToken, Task> coreStartAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new();

    Task RunDetachedAsync<TServices>(
        Func<IServiceProvider, ICollection<IDisposable>, TServices> getServices,
        Func<TServices, CancellationToken, Task> runAsync
    );

    Task WaitForFinishAsync();

    Task<(ExecutionCoord Coord, object Detail)?> GetCurrentAsync(CancellationToken cancellationToken);
}
