using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class PermissionPermissionAssignment : IPermissionAssignment<PermissionPermission>
{
    [JsonProperty("kind")]
    public PermissionKind Kind => PermissionKind.Permission;

    public PermissionPermission Permission { get; }

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public PermissionPermissionAssignment(PermissionPermission permission, Guid? principalId = null)
    {
        Permission = permission;
        PrincipalId = principalId;
    }
}
