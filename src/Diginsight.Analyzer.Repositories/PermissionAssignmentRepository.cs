using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Repositories;

internal sealed partial class PermissionAssignmentRepository : IPermissionAssignmentRepository, IDisposable
{
    private readonly ILogger logger;
    private readonly IRepositoriesOptions repositoriesOptions;
    private readonly Container permissionAssignmentContainer;

    public PermissionAssignmentRepository(
        ILogger<PermissionAssignmentRepository> logger,
        IOptions<RepositoriesOptions> repositoriesOptions
    )
    {
        this.logger = logger;
        this.repositoriesOptions = repositoriesOptions.Value;
        permissionAssignmentContainer = this.repositoriesOptions.CosmosClient.GetContainer("analyzer", "permissionAssignments");
    }

    void IDisposable.Dispose()
    {
        repositoriesOptions.Dispose();
    }

    public async IAsyncEnumerable<IPermissionAssignment<TPermissions>> GetPermissionAssignmentsAE<TPermissions>(
        PermissionKind kind, IEnumerable<Guid> principalIds, [EnumeratorCancellation] CancellationToken cancellationToken
    )
        where TPermissions : struct, IPermission<TPermissions>
    {
        LogMessages.GettingPermissionAssignments(logger, kind, principalIds);

        IQueryable<IPermissionAssignment<TPermissions>> queryable = permissionAssignmentContainer
            .GetItemLinqQueryable<IPermissionAssignment<TPermissions>>(
                requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(kind.ToString("G")) }
            )
            .Where(x => x.PrincipalId == null || principalIds.Contains(x.PrincipalId!.Value));

        using FeedIterator<IPermissionAssignment<TPermissions>> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (IPermissionAssignment<TPermissions> assignment in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return assignment;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IQueryable<T> Log<T>(IQueryable<T> queryable)
    {
        LogMessages.Query(logger, queryable.ToString()!);
        return queryable;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Trace, "Getting {Kind} permission assignments for principal {PrincipalIds}")]
        internal static partial void GettingPermissionAssignments(ILogger logger, PermissionKind kind, IEnumerable<Guid> principalIds);

        [LoggerMessage(1, LogLevel.Trace, "Query: {Queryable}")]
        internal static partial void Query(ILogger logger, string queryable);
    }
}
