namespace Diginsight.Analyzer.Repositories;

public interface ILeaseRepository
{
    Task UpsertAsync(Lease lease);

    Task DeleteAsync(Lease lease);

    IAsyncEnumerable<Lease> GetActiveExceptAE(string leaseId, CancellationToken cancellationToken);

    IAsyncEnumerable<Lease> GetAllAE(CancellationToken cancellationToken);

    IAsyncEnumerable<Lease> GetActiveOrFromPoolAE(string agentPool, CancellationToken cancellationToken);
}
