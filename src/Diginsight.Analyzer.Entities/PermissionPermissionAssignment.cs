using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public readonly struct PermissionPermissionAssignment : IPermissionAssignment<PermissionPermission, object?>
{
    [JsonProperty("kind")]
    public PermissionKind Kind => PermissionKind.Permission;

    public PermissionPermission Permission { get; }

    object IPermissionAssignment.Permission => Permission;

    public object? SubjectId => null;

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public PermissionPermissionAssignment(PermissionPermission permission, Guid? principalId)
    {
        Permission = permission;
        PrincipalId = principalId;
    }
}
