using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public readonly struct AnalysisPermissionAssignment : IPermissionAssignment<AnalysisPermission, Guid>
{
    public PermissionKind Kind => PermissionKind.Analysis;

    public AnalysisPermission Permission { get; }

    [JsonProperty("subjectId")]
    public Guid? SubjectId { get; }

    [JsonConstructor]
    public AnalysisPermissionAssignment(AnalysisPermission permission, Guid? subjectId)
    {
        Permission = permission;
        SubjectId = subjectId;
    }

    public bool NeedsEnabler() => SubjectId is not null && (Permission == AnalysisPermission.Read || Permission == AnalysisPermission.ReadAndExecute);
}
