using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System.Net;
using System.Security.Claims;

namespace Diginsight.Analyzer.API.Services;

internal sealed class PermissionService : IPermissionService
{
    private static readonly AnalysisException UnauthenticatedException = new ("Request not authenticated", HttpStatusCode.Unauthorized, "Unauthenticated");

    private readonly IPermissionAssignmentRepository permissionAssignmentRepository;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly GraphServiceClient graphClient;

    public PermissionService(
        IPermissionAssignmentRepository permissionAssignmentRepository,
        IHttpContextAccessor httpContextAccessor
    )
    {
        this.permissionAssignmentRepository = permissionAssignmentRepository;
        this.httpContextAccessor = httpContextAccessor;
        throw new NotImplementedException();
    }

    private Task<IEnumerable<Guid>> GetCurrentPrincipalIdsAsync(CancellationToken cancellationToken)
    {
        ClaimsPrincipal user = httpContextAccessor.HttpContext?.User ?? throw UnauthenticatedException;
        Claim objectIdClaim = user.FindFirst(ClaimConstants.Oid) ?? user.FindFirst(ClaimConstants.ObjectId) ?? throw UnauthenticatedException;
        if (!Guid.TryParse(objectIdClaim.Value, out Guid objectId))
            throw UnauthenticatedException;

        throw new NotImplementedException();
    }

    public async Task<bool> CanStartAnalysisAsync(CancellationToken cancellationToken)
    {
        IEnumerable<Guid> principalIds = await GetCurrentPrincipalIdsAsync(cancellationToken);

        return await permissionAssignmentRepository
            .GetPermissionAssignmentsAE_S<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, null, cancellationToken)
            .AnyAsync(static x => x.Permission.CanStart, cancellationToken);
    }

    public async Task<IEnumerable<Guid>> GetAnalysesNotReadableAsync(IEnumerable<Guid> analysisIds, CancellationToken cancellationToken)
    {
        IEnumerable<Guid> principalIds = await GetCurrentPrincipalIdsAsync(cancellationToken);

        IEnumerable<Guid?> foundAnalysisIds = await permissionAssignmentRepository
            .GetPermissionAssignmentsAE_S<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, analysisIds, cancellationToken)
            .Where(static x => x.Permission.CanRead)
            .Select(static x => x.SubjectId)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return foundAnalysisIds.Contains(null) ? [ ] : analysisIds.Except(foundAnalysisIds.Cast<Guid>()).ToArray();
    }

    public async Task<bool> CanExecuteAnalysisAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        IEnumerable<Guid> principalIds = await GetCurrentPrincipalIdsAsync(cancellationToken);

        return await permissionAssignmentRepository
            .GetPermissionAssignmentsAE_S<AnalysisPermission, Guid>(PermissionKind.Analysis, principalIds, null, cancellationToken)
            .AnyAsync(static x => x.Permission.CanExecute, cancellationToken);
    }

    public async Task<bool> CanManagePermissionsAsync(CancellationToken cancellationToken)
    {
        IEnumerable<Guid> principalIds = await GetCurrentPrincipalIdsAsync(cancellationToken);

        return await permissionAssignmentRepository
            .GetPermissionAssignmentsAE_C<PermissionPermission, object>(PermissionKind.Permission, principalIds, null, cancellationToken)
            .AnyAsync(static x => x.Permission.CanManage, cancellationToken);
    }
}
