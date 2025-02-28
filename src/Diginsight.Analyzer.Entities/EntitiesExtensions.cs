using Diginsight.Analyzer.Entities.Permissions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Entities;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class EntitiesExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fail(this IFailable failable, string reason)
    {
        failable.Fail(new FailReason(reason));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Skip(this ISkippable skippable, string reason)
    {
        skippable.Skip(new SkipReason(reason));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Deconstruct(this IPermissionAssignment permissionAssignment, out PermissionKind kind, out Guid? principalId, out string permission)
    {
        kind = permissionAssignment.Kind;
        principalId = permissionAssignment.PrincipalId;
        permission = permissionAssignment.Permission;
    }
}
