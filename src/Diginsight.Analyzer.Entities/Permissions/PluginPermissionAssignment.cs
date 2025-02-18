using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public readonly struct PluginPermissionAssignment : IPermissionAssignment<PluginPermission, ValueTuple>
{
    [JsonProperty("kind")]
    public PermissionKind Kind => PermissionKind.Plugin;

    public PluginPermission Permission { get; }

    [JsonProperty("subjectId")]
    public ValueTuple? SubjectId => null;

    [JsonConstructor]
    public PluginPermissionAssignment(PluginPermission permission)
    {
        Permission = permission;
    }

    public bool NeedsEnabler() => false;
}
