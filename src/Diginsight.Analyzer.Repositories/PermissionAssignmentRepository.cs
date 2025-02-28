using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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

    public async IAsyncEnumerable<IPermissionAssignment> GetPermissionAssignmentsAE(
        ValueTuple<PermissionKind>? optionalKind, ValueTuple<Guid?>? optionalPrincipalId, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        LogMessages.GettingPermissionAssignments(logger);

        IQueryable<IPermissionAssignment> queryable = permissionAssignmentContainer
            .GetItemLinqQueryable<IPermissionAssignment>(
                requestOptions: optionalKind is { Item1: var kind }
                    ? new QueryRequestOptions() { PartitionKey = new PartitionKey(kind.ToString("G")) }
                    : null
            );
        if (optionalPrincipalId is { Item1: var principalId })
        {
            queryable = queryable.Where(x => x.PrincipalId == principalId);
        }

        using FeedIterator<IPermissionAssignment> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (IPermissionAssignment assignment in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return assignment;
            }
        }
    }

    public async Task EnsurePermissionAssignmentAsync(IPermissionAssignment permissionAssignment, CancellationToken cancellationToken)
    {
        (PermissionKind kind, Guid? principalId, string permission) = permissionAssignment;

        LogMessages.EnsuringPermissionAssignment(logger, kind, permission, principalId);

        PartitionKey partitionKey = new (kind.ToString("G"));
        IQueryable<IPermissionAssignment> queryable = permissionAssignmentContainer
            .GetItemLinqQueryable<IPermissionAssignment>(
                requestOptions: new QueryRequestOptions() { PartitionKey = partitionKey }
            )
            .Where(x => x.Permission == permission && x.PrincipalId == principalId)
            .Take(1);

        using FeedIterator<IPermissionAssignment> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            if ((await feedIterator.ReadNextAsync(cancellationToken)).Any())
            {
                return;
            }
        }

        await permissionAssignmentContainer.CreateItemAsync(new PermissionAssignmentDocument(permissionAssignment), partitionKey, cancellationToken: cancellationToken);
    }

    public async Task DeletePermissionAssignmentAsync(IPermissionAssignment permissionAssignment, CancellationToken cancellationToken)
    {
        (PermissionKind kind, Guid? principalId, string permission) = permissionAssignment;

        LogMessages.DeletingPermissionAssignment(logger, kind, permission, principalId);

        PartitionKey partitionKey = new (kind.ToString("G"));
        IQueryable<PermissionAssignmentDocument> queryable = permissionAssignmentContainer
            .GetItemLinqQueryable<PermissionAssignmentDocument>(
                requestOptions: new QueryRequestOptions() { PartitionKey = partitionKey }
            )
            .Where(x => x.Permission == permission && x.PrincipalId == principalId)
            .Take(1);

        using FeedIterator<PermissionAssignmentDocument> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            if ((await feedIterator.ReadNextAsync(cancellationToken)).FirstOrDefault() is not { Id: var documentId })
                continue;

            await permissionAssignmentContainer.DeleteItemStreamAsync(documentId!, partitionKey, cancellationToken: cancellationToken);
            return;
        }
    }

    private sealed class PermissionAssignmentDocument : IPermissionAssignment
    {
        public string? Id { get; }

        public PermissionKind Kind { get; }

        [JsonProperty("principalId")]
        public Guid? PrincipalId { get; }

        [JsonProperty("permission")]
        public string Permission { get; }

        public PermissionAssignmentDocument(IPermissionAssignment assignment)
        {
            (Kind, PrincipalId, Permission) = assignment;
        }

        [JsonConstructor]
        private PermissionAssignmentDocument(string id, PermissionKind kind, Guid? principalId, string permission)
        {
            Id = id;
            Kind = kind;
            PrincipalId = principalId;
            Permission = permission;
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

        [LoggerMessage(2, LogLevel.Trace, "Ensuring {Kind} {Permission} permission assignment for principal {PrincipalId}")]
        internal static partial void EnsuringPermissionAssignment(ILogger logger, PermissionKind kind, string permission, Guid? principalId);

        [LoggerMessage(3, LogLevel.Trace, "Deleting {Kind} {Permission} permission assignment for principal {PrincipalId}")]
        internal static partial void DeletingPermissionAssignment(ILogger logger, PermissionKind kind, string permission, Guid? principalId);

        [LoggerMessage(4, LogLevel.Trace, "Getting general permission assignments")]
        internal static partial void GettingPermissionAssignments(ILogger logger);
    }
}
