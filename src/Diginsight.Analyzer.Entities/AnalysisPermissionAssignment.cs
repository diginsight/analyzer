using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public readonly struct AnalysisPermissionAssignment : IPermissionAssignment
{
    public AnalysisPermission Permission { get; }

    public Guid? AnalysisId { get; }

    public Guid? PrincipalId { get; }

    PermissionSubjectKind IPermissionAssignment.Kind => PermissionSubjectKind.Analysis;

    object IPermissionAssignment.Permission => Permission;

    object? IPermissionAssignment.SubjectId => AnalysisId;

    [JsonConstructor]
    public AnalysisPermissionAssignment(AnalysisPermission permission, Guid? analysisId, Guid? principalId)
    {
        Permission = permission;
        AnalysisId = analysisId;
        PrincipalId = principalId;
    }
}
