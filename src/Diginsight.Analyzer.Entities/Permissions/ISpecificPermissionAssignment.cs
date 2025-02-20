using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public interface ISpecificPermissionAssignment
{
    [JsonProperty("principalId")]
    Guid? PrincipalId { get; }

    bool NeedsEnabler();

    bool IsEnabledBy(IEnumerable<IPermissionAssignmentEnabler> enablers);
}

public interface ISpecificPermissionAssignment<out TPermission> : ISpecificPermissionAssignment
    where TPermission : struct, IPermission<TPermission>
{
    TPermission Permission { get; }

    bool ISpecificPermissionAssignment.IsEnabledBy(IEnumerable<IPermissionAssignmentEnabler> enablers)
    {
        return !NeedsEnabler() || enablers.Any(x => x is IPermissionAssignmentEnabler<TPermission> enabler0 && enabler0.Permission >> Permission);
    }
}
