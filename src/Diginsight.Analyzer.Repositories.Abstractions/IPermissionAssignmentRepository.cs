namespace Diginsight.Analyzer.Repositories;

public interface IPermissionAssignmentRepository
{
    IAsyncEnumerable<IPermissionAssignment<TPermissions, TSubject?>> GetPermissionAssignmentsAE_C<TPermissions, TSubject>(
        PermissionKind kind, IEnumerable<Guid> principalIds, IEnumerable<TSubject>? subjectIds, CancellationToken cancellationToken
    )
        where TPermissions : IPermission<TPermissions>
        where TSubject : class;

    IAsyncEnumerable<IPermissionAssignment<TPermissions, TSubject?>> GetPermissionAssignmentsAE_S<TPermissions, TSubject>(
        PermissionKind kind, IEnumerable<Guid> principalIds, IEnumerable<TSubject>? subjectIds, CancellationToken cancellationToken
    )
        where TPermissions : IPermission<TPermissions>
        where TSubject : struct;
}
