using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class AnalysisPermissionAssignment : IPermissionAssignment<AnalysisPermission>
{
    public PermissionKind Kind => PermissionKind.Analysis;

    public AnalysisPermission Permission { get; }

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public AnalysisPermissionAssignment(AnalysisPermission permission, Guid? principalId = null)
    {
        Permission = permission;
        PrincipalId = principalId;
    }
}
