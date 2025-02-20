using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class AnalysisSpecificPermissionAssignment : AnalysisPermissionAssignment, ISpecificPermissionAssignment<AnalysisPermission>
{
    [JsonConstructor]
    public AnalysisSpecificPermissionAssignment(AnalysisPermission permission, Guid? principalId)
        : base(permission, principalId) { }

    public bool NeedsEnabler() => Permission == AnalysisPermission.Read || Permission == AnalysisPermission.ReadAndInvoke;
}
