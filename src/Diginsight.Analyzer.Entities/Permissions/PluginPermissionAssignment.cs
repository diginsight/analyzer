using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class PluginPermissionAssignment : IPermissionAssignment<PluginPermission>
{
    [JsonProperty("kind")]
    public PermissionKind Kind => PermissionKind.Plugin;

    public PluginPermission Permission { get; }

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public PluginPermissionAssignment(PluginPermission permission, Guid? principalId = null)
    {
        Permission = permission;
        PrincipalId = principalId;
    }
}
