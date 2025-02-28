using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public sealed class DummyPermissionService : IPermissionService
{
    public Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CheckCanDequeueExecutionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CheckCanDequeueExecutionAsync(IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task CheckCanReadAnalysisAsync(IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task CheckCanInvokeAnalysisAsync(IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IQueryable<AnalysisContextSnapshot>> WhereCanReadAnalysisAsync(IQueryable<AnalysisContextSnapshot> queryable, CancellationToken cancellationToken)
        => Task.FromResult(queryable);

    public Task CheckCanManagePluginsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IAsyncEnumerable<IPermissionAssignment> GetPermissionAssignmentsAE(
        ValueTuple<PermissionKind>? optionalKind, ValueTuple<Guid?>? optionalPrincipalId, CancellationToken cancellationToken
    )
    {
        return AsyncEnumerable.Empty<IPermissionAssignment>();
    }

    public Task AssignPermissionAsync(IPermissionAssignment assignment, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemovePermissionAsync(IPermissionAssignment assignment, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AssignAnalysisPermissionAsync(Guid executionId, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task AssignAnalysisPermissionAsync(AnalysisCoord coord, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task RemoveAnalysisPermissionAsync(Guid executionId, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task RemoveAnalysisPermissionAsync(AnalysisCoord coord, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
