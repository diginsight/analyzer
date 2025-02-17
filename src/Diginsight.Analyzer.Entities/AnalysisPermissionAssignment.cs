using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public readonly struct AnalysisPermissionAssignment : IPermissionAssignment<AnalysisPermission, Guid?>
{
    public PermissionKind Kind => PermissionKind.Analysis;

    public AnalysisPermission Permission { get; }

    object IPermissionAssignment.Permission => Permission;

    public Guid? SubjectId { get; }

    public Guid? PrincipalId { get; }

    [JsonConstructor]
    public AnalysisPermissionAssignment(AnalysisPermission permission, Guid? subjectId, Guid? principalId)
    {
        Permission = permission;
        SubjectId = subjectId;
        PrincipalId = principalId;
    }
}
