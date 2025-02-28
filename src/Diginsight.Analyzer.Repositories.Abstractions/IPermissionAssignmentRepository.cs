using Diginsight.Analyzer.Entities.Permissions;

namespace Diginsight.Analyzer.Repositories;

public interface IPermissionAssignmentRepository
{
    IAsyncEnumerable<IPermissionAssignment<TPermissions>> GetPermissionAssignmentsAE<TPermissions>(
        PermissionKind kind, IEnumerable<Guid> principalIds, CancellationToken cancellationToken
    )
        where TPermissions : struct, IPermission<TPermissions>;

    IAsyncEnumerable<IPermissionAssignment> GetPermissionAssignmentsAE(
        ValueTuple<PermissionKind>? optionalKind, ValueTuple<Guid?>? optionalPrincipalId, CancellationToken cancellationToken
    );

    Task EnsurePermissionAssignmentAsync(IPermissionAssignment assignment, CancellationToken cancellationToken);

    Task DeletePermissionAssignmentAsync(IPermissionAssignment assignment, CancellationToken cancellationToken);
}
