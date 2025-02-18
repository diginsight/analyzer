namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class AnalysisPermissionAssignmentEnabler : IPermissionAssignmentEnabler<AnalysisPermission>
{
    public IEnumerable<IPermissionAssignment> StaticAssignments { get; } = [ ];

    public AnalysisPermission Permission { get; }

    public AnalysisPermissionAssignmentEnabler(AnalysisPermission permission)
    {
        Permission = permission;
    }
}
