using Diginsight.Analyzer.Entities.Permissions;

namespace Diginsight.Analyzer.Repositories;

public interface IPermissionAssignmentRepository
{
    IAsyncEnumerable<IPermissionAssignment<TPermissions>> GetPermissionAssignmentsAE<TPermissions>(
        PermissionKind kind, IEnumerable<Guid> principalIds, CancellationToken cancellationToken
    )
        where TPermissions : struct, IPermission<TPermissions>;
}
