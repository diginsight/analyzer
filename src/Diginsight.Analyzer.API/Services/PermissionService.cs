using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Permissions;
using Diginsight.Analyzer.Repositories;
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

    private static readonly AnalysisException CannotStartAnalysisException =
        new ("Cannot start a new analysis", HttpStatusCode.Forbidden, "CannotStartAnalysis");

    private static readonly AnalysisException CannotReadAnalysisException =
        new ("Cannot read such analysis", HttpStatusCode.Forbidden, "CannotReadAnalysis");

    private static readonly AnalysisException CannotInvokeAnalysisException =
        new ("Cannot invoke on such analysis", HttpStatusCode.Forbidden, "CannotInvokeAnalysis");

    private static readonly AnalysisException CannotManagePermissionsException =
        new ("Cannot manage permissions", HttpStatusCode.Forbidden, "CannotManagePermissions");

    private static readonly AnalysisException CannotManagePluginsException =
        new ("Cannot manage plugins", HttpStatusCode.Forbidden, "CannotManagePlugins");

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
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.Start, null));
        }
        if (rawPermissions.Contains("Analyses.Read"))
        {
            enablers.Add(new AnalysisPermissionAssignmentEnabler(AnalysisPermission.Read));
        }
        if (rawPermissions.Contains("Analyses.ReadAll"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.Read, null));
        }
        if (rawPermissions.Contains("Analyses.Invoke"))
        {
            enablers.Add(new AnalysisPermissionAssignmentEnabler(AnalysisPermission.ReadAndInvoke));
        }
        if (rawPermissions.Contains("Analyses.InvokeAll"))
        {
            staticAssignments.Add(new AnalysisPermissionAssignment(AnalysisPermission.ReadAndInvoke, null));
        }
        if (rawPermissions.Contains("Permissions.ManageAll"))
        {
            staticAssignments.Add(new PermissionPermissionAssignment(PermissionPermission.Manage));
        }

        httpContext.Items[PermissionAssignmentEnablersItemKey] = enablers;
        return enablers;
    }

    private sealed class StaticPermissionAssignmentEnabler : IPermissionAssignmentEnabler
    {
        public required IEnumerable<IPermissionAssignment> StaticAssignments { get; init; }
    }

    private async Task<IEnumerable<Guid>> GetPrincipalIdsAsync(CancellationToken cancellationToken)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext ?? throw UnauthenticatedException;
        if (httpContext.Items.TryGetValue(PrincipalIdsItemKey, out object? rawPrincipalIds))
        {
            return (IEnumerable<Guid>)rawPrincipalIds!;
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

        IEnumerable<Guid> principalIds = (await identityRepository.GetGroupsAsync(objectId, isUser, cancellationToken)).Prepend(objectId).ToArray();
        httpContext.Items[PrincipalIdsItemKey] = principalIds;
        return principalIds;
    }

    public async Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<AnalysisPermission> pa) => pa.Permission.CanStart;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<AnalysisPermission>>().Any(Matches))
        {
            return;
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, null, cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .AnyAsync(Matches, cancellationToken))
        {
            return;
        }

        throw CannotStartAnalysisException;
    }

    public async Task CheckCanReadAnalysisAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<AnalysisPermission> pa) => pa.Permission.CanRead;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<AnalysisPermission>>().Any(Matches))
        {
            return;
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, [ analysisId ], cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .AnyAsync(Matches, cancellationToken))
        {
            return;
        }

        throw CannotReadAnalysisException;
    }

    public async Task<IEnumerable<Guid>> FilterReadableAnalysesAsync(IEnumerable<Guid> analysisIds, CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<AnalysisPermission> pa) => pa.Permission.CanRead;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<AnalysisPermission>>().Any(Matches))
        {
            return [ ];
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        IEnumerable<Guid?> foundAnalysisIds = await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, analysisIds, cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .Where(Matches)
            .Select(static x => x.SubjectId)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return foundAnalysisIds.Contains(null) ? analysisIds : analysisIds.Intersect(foundAnalysisIds.Cast<Guid>()).ToArray();
    }

    public async Task CheckCanInvokeAnalysisAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<AnalysisPermission> pa) => pa.Permission.CanInvoke;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<AnalysisPermission>>().Any(Matches))
        {
            return;
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, [ analysisId ], cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .AnyAsync(Matches, cancellationToken))
        {
            return;
        }

        throw CannotInvokeAnalysisException;
    }

    public async Task CheckCanManagePermissionsAsync(CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<PermissionPermission> pa) => pa.Permission.CanManage;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<PermissionPermission>>().Any(Matches))
        {
            return;
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<PermissionPermission, ValueTuple>(PermissionKind.Permission, principalIds, null, cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .AnyAsync(Matches, cancellationToken))
        {
            return;
        }

        throw CannotManagePermissionsException;
    }

    public async Task CheckCanManagePluginsAsync(CancellationToken cancellationToken)
    {
        static bool Matches(IPermissionAssignment<PluginPermission> pa) => pa.Permission.CanManage;

        IEnumerable<IPermissionAssignmentEnabler> enablers = GetPermissionAssignmentEnablers();
        if (enablers.SelectMany(static x => x.StaticAssignments).OfType<IPermissionAssignment<PluginPermission>>().Any(Matches))
        {
            return;
        }

        IEnumerable<Guid> principalIds = await GetPrincipalIdsAsync(cancellationToken);

        if (await permissionAssignmentRepository
            .GetPermissionAssignmentsAE<PluginPermission, ValueTuple>(PermissionKind.Plugin, principalIds, null, cancellationToken)
            .Where(x => x.IsEnabledBy(enablers))
            .AnyAsync(Matches, cancellationToken))
        {
            return;
        }

        throw CannotManagePluginsException;
    }
}
