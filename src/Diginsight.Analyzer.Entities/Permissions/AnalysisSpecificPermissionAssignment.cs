using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class AnalysisSpecificPermissionAssignment : ISpecificPermissionAssignment<AnalysisPermission>
{
    public AnalysisPermission Permission { get; }

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public AnalysisSpecificPermissionAssignment(AnalysisPermission permission, Guid? principalId = null)
    {
        Permission = permission;
        PrincipalId = principalId;
    }

    public bool NeedsEnabler() => Permission == AnalysisPermission.Read || Permission == AnalysisPermission.Invoke;
}
