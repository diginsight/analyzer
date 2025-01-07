using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Repositories;

internal sealed partial class LeaseRepository : ILeaseRepository, IDisposable
{
    private readonly ILogger logger;
    private readonly IRepositoriesOptions repositoriesOptions;
    private readonly Container leaseContainer;

    public LeaseRepository(
        ILogger<LeaseRepository> logger,
        IOptions<RepositoriesOptions> repositoriesOptions
    )
    {
        this.logger = logger;
        this.repositoriesOptions = repositoriesOptions.Value;
        leaseContainer = this.repositoriesOptions.CosmosClient.GetContainer("analyzer", "leases");
    }

    void IDisposable.Dispose()
    {
        repositoriesOptions.Dispose();
    }

    public Task UpsertAsync(Lease lease)
    {
        string leaseId = lease.Id;
        LogMessages.UpsertingLease(logger, leaseId);
        return leaseContainer.UpsertItemAsync(
            lease, new PartitionKey(leaseId), new ItemRequestOptions() { EnableContentResponseOnWrite = false }
        );
    }

    public Task DeleteAsync(Lease lease)
    {
        string leaseId = lease.Id;
        LogMessages.DeletingLease(logger, leaseId);
        return leaseContainer.DeleteItemAsync<Lease>(leaseId, new PartitionKey(leaseId));
    }

    public async IAsyncEnumerable<Lease> GetActiveExceptAE(string leaseId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogMessages.GettingActiveLeasesExcept(logger, leaseId);

        IQueryable<Lease> queryable = leaseContainer.GetItemLinqQueryable<Lease>()
            .Where(x => x.Kind != null && x.Id != leaseId);

        using FeedIterator<Lease> feedIterator = queryable.ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (Lease lease in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return lease;
            }
        }
    }

    public async IAsyncEnumerable<Lease> GetAllAE([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogMessages.GettingAllLeases(logger);

        IQueryable<Lease> queryable = leaseContainer.GetItemLinqQueryable<Lease>();

        using FeedIterator<Lease> feedIterator = queryable.ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (Lease lease in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return lease;
            }
        }
    }

    public async IAsyncEnumerable<Lease> GetActiveOrFromPoolAE(string agentPool, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogMessages.GettingLeasesActiveOrFromPool(logger, agentPool);

        IQueryable<Lease> queryable = leaseContainer.GetItemLinqQueryable<Lease>()
            .Where(x => x.Kind != null || x.AgentPool == agentPool);

        using FeedIterator<Lease> feedIterator = queryable.ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (Lease lease in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return lease;
            }
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Trace, "Upserting lease {LeaseId}")]
        internal static partial void UpsertingLease(ILogger logger, string leaseId);

        [LoggerMessage(1, LogLevel.Trace, "Deleting lease {LeaseId}")]
        internal static partial void DeletingLease(ILogger logger, string leaseId);

        [LoggerMessage(2, LogLevel.Trace, "Getting active lease except {LeaseId}")]
        internal static partial void GettingActiveLeasesExcept(ILogger logger, string leaseId);

        [LoggerMessage(3, LogLevel.Trace, "Getting all leases")]
        internal static partial void GettingAllLeases(ILogger logger);

        [LoggerMessage(4, LogLevel.Trace, "Getting leases active or from pool {AgentPool}")]
        internal static partial void GettingLeasesActiveOrFromPool(ILogger logger, string agentPool);
    }
}
