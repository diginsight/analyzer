using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public interface IPermissionService
{
    Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken);

    Task CheckCanDequeueExecutionsAsync(CancellationToken cancellationToken);

    Task CheckCanDequeueExecutionAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken
    );

    Task CheckCanReadAnalysisAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken
    );

    Task CheckCanInvokeAnalysisAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignment, CancellationToken cancellationToken
    );

    Task<IQueryable<AnalysisContextSnapshot>> WhereCanReadAnalysisAsync(IQueryable<AnalysisContextSnapshot> queryable, CancellationToken cancellationToken);

    Task CheckCanManagePluginsAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IPermissionAssignment> GetPermissionAssignmentsAE(
        ValueTuple<PermissionKind>? optionalKind, ValueTuple<Guid?>? optionalPrincipalId, CancellationToken cancellationToken
    );

    Task AssignPermissionAsync(IPermissionAssignment assignment, CancellationToken cancellationToken);

    Task RemovePermissionAsync(IPermissionAssignment assignment, CancellationToken cancellationToken);

    Task AssignAnalysisPermissionAsync(Guid executionId, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken);

    Task AssignAnalysisPermissionAsync(AnalysisCoord coord, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken);

    Task RemoveAnalysisPermissionAsync(Guid executionId, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken);

    Task RemoveAnalysisPermissionAsync(AnalysisCoord coord, AnalysisSpecificPermissionAssignment assignment, CancellationToken cancellationToken);
}
