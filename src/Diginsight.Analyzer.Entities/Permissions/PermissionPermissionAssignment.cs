using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public readonly struct PermissionPermissionAssignment : IPermissionAssignment<PermissionPermission, ValueTuple>
{
    [JsonProperty("kind")]
    public PermissionKind Kind => PermissionKind.Permission;

    public PermissionPermission Permission { get; }

    [JsonProperty("subjectId")]
    public ValueTuple? SubjectId => null;

    [JsonConstructor]
    public PermissionPermissionAssignment(PermissionPermission permission)
    {
        Permission = permission;
    }

    public bool NeedsEnabler() => false;
}
