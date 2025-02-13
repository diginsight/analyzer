using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public readonly struct PermissionPermissionAssignment : IPermissionAssignment
{
    public PermissionPermission Permission { get; }

    public Guid? PrincipalId { get; }

    PermissionSubjectKind IPermissionAssignment.Kind => PermissionSubjectKind.Permission;

    object IPermissionAssignment.Permission => Permission;

    object? IPermissionAssignment.SubjectId => null;

    [JsonConstructor]
    public PermissionPermissionAssignment(PermissionPermission permission, Guid? principalId)
    {
        Permission = permission;
        PrincipalId = principalId;
    }
}
