using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Identity.Web;
using System.Net;
using System.Security.Claims;

namespace Diginsight.Analyzer.API.Services;

// TODO Intercept 401 and 403 in orchestrator
internal sealed class PermissionService : IPermissionService
{
    private static readonly AnalysisException UnauthenticatedException =
        new ("Not authenticated", HttpStatusCode.Unauthorized, "Unauthenticated");

    private static readonly AnalysisException BadOidClaimException =
        new ("Object id claim is missing or malformed", HttpStatusCode.Unauthorized, "BadOidClaim");

    private static readonly AnalysisException BadAzpacrClaimException =
        new ("Authentication method claim is missing or malformed", HttpStatusCode.Unauthorized, "BadAzpacrClaim");

    private static readonly AnalysisException BadAzpClaimException =
        new ("App id claim is missing or malformed", HttpStatusCode.Unauthorized, "BadAzpClaim");

    private static readonly AnalysisException CannotStartAnalysisException =
        new ("Cannot start a new analysis", HttpStatusCode.Forbidden, "CannotStartAnalysis");

    private static readonly AnalysisException CannotDequeueExecutionsException =
        new ("Cannot dequeue executions", HttpStatusCode.Forbidden, "CannotDequeueExecutions");

    private static readonly AnalysisException CannotReadAnalysisException =
        new ("Cannot read such analysis", HttpStatusCode.Forbidden, "CannotReadAnalysis");

    private static readonly AnalysisException CannotInvokeAnalysisException =
        new ("Cannot invoke on such analysis", HttpStatusCode.Forbidden, "CannotInvokeAnalysis");

    private static readonly AnalysisException CannotManagePermissionsException =
        new ("Cannot manage permissions", HttpStatusCode.Forbidden, "CannotManagePermissions");

    private static readonly AnalysisException CannotManagePluginsException =
        new ("Cannot manage plugins", HttpStatusCode.Forbidden, "CannotManagePlugins");

    private static readonly object MainPrincipalItemKey = new ();
    private static readonly object PrincipalIdsItemKey = new ();
    private static readonly object PermissionAssignmentEnablersItemKey = new ();

    private readonly IPermissionAssignmentRepository permissionAssignmentRepository;
    private readonly IIdentityRepository identityRepository;
    private readonly IHttpContextAccessor httpContextAccessor;

    public PermissionService(
        IPermissionAssignmentRepository permissionAssignmentRepository,
        IIdentityRepository identityRepository,
        IHttpContextAccessor httpContextAccessor
    )
    {
        this.permissionAssignmentRepository = permissionAssignmentRepository;
        this.identityRepository = identityRepository;
        this.httpContextAccessor = httpContextAccessor;
    }

    private IEnumerable<IPermissionAssignmentEnabler> GetPermissionAssignmentEnablers()
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw UnauthenticatedException;
        if (httpContext.Items.TryGetValue(PermissionAssignmentEnablersItemKey, out object? rawPermissionAssignments))
        {
            return (IEnumerable<IPermissionAssignmentEnabler>)rawPermissionAssignments!;
        }

        ClaimsPrincipal user = httpContext.User ?? throw UnauthenticatedException;

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
            enablers.Add(new AnalysisPermissionAssignmentEnabler(AnalysisPermission.ReadAndInvoke));
        }
        if (rawPermissions.Contains("Analyses.InvokeAll"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.ReadAndInvoke));
        }
        if (rawPermissions.Contains("Permissions.ManageAll"))
        {
            staticAssignments.Add(new PermissionPermissionAssignment(PermissionPermission.Manage));
        }
        if (rawPermissions.Contains("Plugins.ManageAll"))
        {
            staticAssignments.Add(new PluginPermissionAssignment(PluginPermission.Manage));
        }

        httpContext.Items[PermissionAssignmentEnablersItemKey] = enablers;
        return enablers;
    }

    private sealed class StaticPermissionAssignmentEnabler : IPermissionAssignmentEnabler
    {
        public required IEnumerable<IPermissionAssignment> StaticAssignments { get; init; }
    }

    private (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal()
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw UnauthenticatedException;
        if (httpContext.Items.TryGetValue(MainPrincipalItemKey, out object? rawMainPrincipal))
        {
            return (ValueTuple<Guid, Guid?>)rawMainPrincipal!;
        }

        ClaimsPrincipal user = httpContext.User ?? throw UnauthenticatedException;

        Claim objectIdClaim = user.FindFirst(ClaimConstants.Oid) ?? user.FindFirst(ClaimConstants.ObjectId) ?? throw BadOidClaimException;
        if (!Guid.TryParse(objectIdClaim.Value, out Guid objectId))
            throw BadOidClaimException;

        Claim authMethClaim = user.FindFirst("azpacr") ?? user.FindFirst("appidacr") ?? throw BadAzpacrClaimException;
        if (!int.TryParse(authMethClaim.Value, out int authMeth))
            throw BadAzpacrClaimException;

        bool isUser = authMeth switch
        {
            0 => true,
            1 or 2 => false,
            _ => throw BadAzpacrClaimException,
        };

        Guid? maybeAppId;
        if (isUser)
        {
            maybeAppId = null;
        }
        else
        {
            Claim appIdClaim = user.FindFirst("azp") ?? user.FindFirst("Appid") ?? throw BadAzpClaimException;
            if (!Guid.TryParse(appIdClaim.Value, out Guid appId))
                throw BadAzpClaimException;
            maybeAppId = appId;
        }

        (Guid, Guid?) mainPrincipal = (objectId, maybeAppId);
        httpContext.Items[MainPrincipalItemKey] = mainPrincipal;
        return mainPrincipal;
    }

    private async ValueTask<IEnumerable<Guid>> GetPrincipalIdsAsync(CancellationToken cancellationToken)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw UnauthenticatedException;
        if (httpContext.Items.TryGetValue(PrincipalIdsItemKey, out object? rawPrincipalIds))
        {
            return (IEnumerable<Guid>)rawPrincipalIds!;
        }

        (Guid objectId, Guid? appId) = GetMainPrincipal();

        IEnumerable<Guid> principalIds = (await identityRepository.GetGroupIdsAsync(objectId, appId is null, cancellationToken)).Prepend(objectId).ToArray();
        httpContext.Items[PrincipalIdsItemKey] = principalIds;
        return principalIds;
    }

    public Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken)
    {
        return CheckCanDoStuffAsync<AnalysisPermission>(
            PermissionKind.Analysis, static x => x.Permission.CanStart, CannotStartAnalysisException, cancellationToken
        );
    }

    public async Task CheckCanDequeueExecutionsAsync(CancellationToken cancellationToken)
    {
        if (GetMainPrincipal() is not (_, { } appId))
        {
            // TODO Validate appId equal to self
            throw CannotDequeueExecutionsException;
        }
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
                        (pa.Permission == nameof(AnalysisPermission.Read) || pa.Permission == nameof(AnalysisPermission.ReadAndInvoke))
                )
            )
            : queryable;
    }

    public Task CheckCanManagePermissionsAsync(CancellationToken cancellationToken)
    {
        return CheckCanDoStuffAsync<PermissionPermission>(
            PermissionKind.Permission, static x => x.Permission.CanManage, CannotManagePermissionsException, cancellationToken
        );
    }

    public Task CheckCanManagePluginsAsync(CancellationToken cancellationToken)
    {
        return CheckCanDoStuffAsync<PluginPermission>(
            PermissionKind.Plugin, static x => x.Permission.CanManage, CannotManagePluginsException, cancellationToken
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

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

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
