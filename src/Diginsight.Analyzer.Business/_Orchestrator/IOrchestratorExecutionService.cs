namespace Diginsight.Analyzer.Business;

internal interface IOrchestratorExecutionService
{
    public const string NoAgentAvailableExceptionLabel = "NoAgentAvailable";

    Task<(Guid Id, object Detail)> StartAsync(
        string? agentPool,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<IAgentClient, CancellationToken, Task<(Guid Id, object Detail)>> coreStartAsync,
        CancellationToken cancellationToken
    );

    Task<bool> DequeueAsync(Guid executionId, string agentPool, CancellationToken cancellationToken);

    IAsyncEnumerable<(Guid Id, object Detail)> AbortAE(
        ExecutionKind kind, Guid? executionId, CancellationToken cancellationToken
    );
}
