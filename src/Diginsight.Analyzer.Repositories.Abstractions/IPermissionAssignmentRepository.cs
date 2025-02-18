using Diginsight.Analyzer.Entities.Permissions;

namespace Diginsight.Analyzer.Repositories;

public interface IPermissionAssignmentRepository
{
    IAsyncEnumerable<IPermissionAssignment<TPermissions, TSubject>> GetPermissionAssignmentsAE<TPermissions, TSubject>(
        PermissionKind kind, IEnumerable<Guid> principalIds, IEnumerable<TSubject>? subjectIds, CancellationToken cancellationToken
    )
        where TPermissions : struct, IPermission<TPermissions>
        where TSubject : struct, IEquatable<TSubject>;
}
