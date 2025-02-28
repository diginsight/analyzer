using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace Diginsight.Analyzer.API.Services;

// TODO Intercept 401 and 403 in orchestrator
internal sealed class PermissionService : IPermissionService
{
    private static readonly AnalysisException CannotStartAnalysisException =
        new ("Cannot start a new analysis", HttpStatusCode.Forbidden, "CannotStartAnalysis");

    private static readonly AnalysisException CannotDequeueExecutionsException =
        new ("Cannot dequeue executions", HttpStatusCode.Forbidden, "CannotDequeueExecutions");

    private static readonly AnalysisException CannotReadAnalysisException =
        new ("Cannot read such analysis", HttpStatusCode.Forbidden, "CannotReadAnalysis");

    private static readonly AnalysisException CannotInvokeAnalysisException =
        new ("Cannot invoke on such analysis", HttpStatusCode.Forbidden, "CannotInvokeAnalysis");

    private static readonly AnalysisException CannotReadPermissionsException =
        new ("Cannot read permissions", HttpStatusCode.Forbidden, "CannotReadPermissions");

    private static readonly AnalysisException CannotManagePermissionsException =
        new ("Cannot manage permissions", HttpStatusCode.Forbidden, "CannotManagePermissions");

    private static readonly AnalysisException CannotManagePluginsException =
        new ("Cannot manage plugins", HttpStatusCode.Forbidden, "CannotManagePlugins");

    private static readonly object PermissionAssignmentEnablersItemKey = new ();

    private readonly IPermissionAssignmentRepository permissionAssignmentRepository;
    private readonly IIdentityRepository identityRepository;
    private readonly ICallContextAccessor callContextAccessor;
    private readonly Lazy<Guid> selfAppIdLazy;

    private Guid SelfAppId => selfAppIdLazy.Value;

    public PermissionService(
        IPermissionAssignmentRepository permissionAssignmentRepository,
        IIdentityRepository identityRepository,
        ICallContextAccessor callContextAccessor,
        IOptionsMonitor<JwtBearerOptions> jwtBearerOptionsMonitor
    )
    {
        this.permissionAssignmentRepository = permissionAssignmentRepository;
        this.identityRepository = identityRepository;
        this.callContextAccessor = callContextAccessor;
        selfAppIdLazy = new Lazy<Guid>(() => Guid.Parse(jwtBearerOptionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme).Audience!));
    }

    private IEnumerable<IPermissionAssignmentEnabler> GetPermissionAssignmentEnablers()
    {
        if (callContextAccessor.Items.TryGetValue(PermissionAssignmentEnablersItemKey, out object? rawPermissionAssignments))
        {
            return (IEnumerable<IPermissionAssignmentEnabler>)rawPermissionAssignments!;
        }

        ClaimsPrincipal user = callContextAccessor.User;

        IEnumerable<string> scopes = user.FindAll(ClaimConstants.Scp).Concat(user.FindAll(ClaimConstants.Scope))
            .SelectMany(static x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        IEnumerable<string> roles = user.FindAll(ClaimConstants.Roles).Concat(user.FindAll(ClaimConstants.Role))
            .SelectMany(static x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(static x => x.EndsWith("_as_app", StringComparison.Ordinal) ? x[..^7] : x);

        ICollection<IPermissionAssignment> staticAssignments = new List<IPermissionAssignment>();
        ICollection<IPermissionAssignmentEnabler> enablers = new List<IPermissionAssignmentEnabler>()
        {
            new StaticPermissionAssignmentEnabler() { StaticAssignments = staticAssignments },
        };

        ISet<string> rawPermissions = new HashSet<string>(scopes.Concat(roles));
        if (rawPermissions.Contains("Analyses.Start"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.Start));
        }
        if (rawPermissions.Contains("Analyses.Read"))
        {
            enablers.Add(new AnalysisPermissionAssignmentEnabler(AnalysisPermission.Read));
        }
        if (rawPermissions.Contains("Analyses.ReadAll"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.Read));
        }
        if (rawPermissions.Contains("Analyses.Invoke"))
        {
            enablers.Add(new AnalysisPermissionAssignmentEnabler(AnalysisPermission.Invoke));
        }
        if (rawPermissions.Contains("Analyses.InvokeAll"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.Invoke));
        }
        if (rawPermissions.Contains("Permissions.ReadAll"))
        {
            staticAssignments.Add(new PermissionPermissionAssignment(PermissionPermission.Read));
        }
        if (rawPermissions.Contains("Permissions.ManageAll"))
        {
            staticAssignments.Add(new PermissionPermissionAssignment(PermissionPermission.Manage));
        }
        if (rawPermissions.Contains("Plugins.ManageAll"))
        {
            staticAssignments.Add(new PluginPermissionAssignment(PluginPermission.Manage));
        }

        callContextAccessor.Items[PermissionAssignmentEnablersItemKey] = enablers;
        return enablers;
    }

    private sealed class StaticPermissionAssignmentEnabler : IPermissionAssignmentEnabler
    {
        public required IEnumerable<IPermissionAssignment> StaticAssignments { get; init; }
    }

    public Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken)
    {
        return CheckCanDoStuffAsync<AnalysisPermission>(
            PermissionKind.Analysis, static x => x.Permission.CanStart, CannotStartAnalysisException, cancellationToken
        );
    }

    public Task CheckCanDequeueExecutionsAsync(CancellationToken cancellationToken)
    {
        return identityRepository.GetMainPrincipal() is (_, { } appId) && SelfAppId == appId
            ? Task.CompletedTask
            : Task.FromException(CannotDequeueExecutionsException);
    }

    public Task CheckCanDequeueExecutionAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken
    )
    {
        return CheckCanDoStuffAsync(
            PermissionKind.Analysis, static x => x.Permission.CanInvoke, assignments, CannotStartAnalysisException, cancellationToken
        );
    }

    public Task CheckCanReadAnalysisAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken
    )
    {
        return CheckCanDoStuffAsync(
            PermissionKind.Analysis, static x => x.Permission.CanRead, assignments, CannotReadAnalysisException, cancellationToken
        );
    }

    public Task CheckCanInvokeAnalysisAsync(
        IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> assignments, CancellationToken cancellationToken
    )
    {
        return CheckCanDoStuffAsync(
            PermissionKind.Analysis, static x => x.Permission.CanInvoke, assignments, CannotInvokeAnalysisException, cancellationToken
        );
    }

    public async Task<IQueryable<AnalysisContextSnapshot>> WhereCanReadAnalysisAsync(
        IQueryable<AnalysisContextSnapshot> queryable, CancellationToken cancellationToken
    )
    {
        return await CoreCheckCanDoStuffAsync<AnalysisPermission>(PermissionKind.Analysis, static x => x.Permission.CanRead, cancellationToken) is var (_, principalIds)
            ? queryable.Where(
                x => x.PermissionAssignmentsQO.Any(
                    pa => (pa.PrincipalId == null || principalIds.Contains(pa.PrincipalId.Value)) &&
                        (pa.Permission == nameof(AnalysisPermission.Read) || pa.Permission == nameof(AnalysisPermission.Invoke))
                )
            )
            : queryable;
    }

    public Task CheckCanManagePluginsAsync(CancellationToken cancellationToken)
    {
        return CheckCanDoStuffAsync<PluginPermission>(
            PermissionKind.Plugin, static x => x.Permission.CanManage, CannotManagePluginsException, cancellationToken
        );
    }

    public async IAsyncEnumerable<IPermissionAssignment> GetPermissionAssignmentsAE(
        ValueTuple<PermissionKind>? optionalKind, ValueTuple<Guid?>? optionalPrincipalId, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await CheckCanHandlePermissionsAsync(false, cancellationToken);

        await foreach (IPermissionAssignment assignment in permissionAssignmentRepository.GetPermissionAssignmentsAE(optionalKind, optionalPrincipalId, cancellationToken))
        {
            yield return assignment;
        }
    }

    public async Task AssignPermissionAsync(IPermissionAssignment permissionAssignment, CancellationToken cancellationToken)
    {
        await CheckCanHandlePermissionsAsync(true, cancellationToken);
        await permissionAssignmentRepository.EnsurePermissionAssignmentAsync(permissionAssignment, cancellationToken);
    }

    public async Task DeletePermissionAsync(IPermissionAssignment permissionAssignment, CancellationToken cancellationToken)
    {
        await CheckCanHandlePermissionsAsync(true, cancellationToken);
        await permissionAssignmentRepository.DeletePermissionAssignmentAsync(permissionAssignment, cancellationToken);
    }

    private Task CheckCanHandlePermissionsAsync(bool manage, CancellationToken cancellationToken)
    {
        return identityRepository.GetMainPrincipal() is (_, { } appId) && SelfAppId == appId
            ? Task.CompletedTask
            : CheckCanDoStuffAsync<PermissionPermission>(
                PermissionKind.Permission,
                manage ? static x => x.Permission.CanManage : static x => x.Permission.CanRead,
                manage ? CannotManagePermissionsException : CannotReadPermissionsException,
                cancellationToken
            );
    }

    private async Task<(IEnumerable<IPermissionAssignmentEnabler> Enablers, IEnumerable<Guid> PrincipalId)?> CoreCheckCanDoStuffAsync<TPermission>(
        PermissionKind kind,
        Func<IPermissionAssignment<TPermission>, bool> matches,
        CancellationToken cancellationToken
    )
        where TPermission : struct, IPermission<TPermission>
    {
        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<TPermission>>().Any(matches))
        {
            return null;
        }

        IEnumerable<Guid> principalIds = await identityRepository.GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<TPermission>(kind, principalIds, cancellationToken)
            .AnyAsync(matches, cancellationToken))
        {
            return null;
        }

        return (enablers, principalIds);
    }

    private async Task CheckCanDoStuffAsync<TPermission>(
        PermissionKind kind,
        Func<IPermissionAssignment<TPermission>, bool> matches,
        AnalysisException exception,
        CancellationToken cancellationToken
    )
        where TPermission : struct, IPermission<TPermission>
    {
        if (await CoreCheckCanDoStuffAsync(kind, matches, cancellationToken) is not null)
        {
            throw exception;
        }
    }

    private async Task CheckCanDoStuffAsync<TPermission>(
        PermissionKind kind,
        Func<IPermissionAssignment<TPermission>, bool> matches,
        IEnumerable<ISpecificPermissionAssignment<TPermission>> assignments,
        AnalysisException exception,
        CancellationToken cancellationToken
    )
        where TPermission : struct, IPermission<TPermission>
    {
        if (await CoreCheckCanDoStuffAsync(kind, matches, cancellationToken) is var (enablers, _) &&
            !assignments.Any(x => x.IsEnabledBy(enablers)))
        {
            throw exception;
        }
    }
}
