namespace Diginsight.Analyzer.Entities.Permissions;

public interface IPermissionAssignmentEnabler
{
    IEnumerable<IPermissionAssignment> StaticAssignments { get; }
}

public interface IPermissionAssignmentEnabler<out TPermission> : IPermissionAssignmentEnabler
    where TPermission : struct, IPermission<TPermission>
{
    TPermission Permission { get; }
}
