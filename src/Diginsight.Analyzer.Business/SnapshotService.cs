using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed class SnapshotService : ISnapshotService
{
    private readonly IPermissionService permissionService;
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly ICoreOptions coreOptions;

    public SnapshotService(
        IPermissionService permissionService,
        IAnalysisInfoRepository infoRepository,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.permissionService = permissionService;
        this.infoRepository = infoRepository;
        this.coreOptions = coreOptions.Value;
    }

    public Task<Page<AnalysisContextSnapshot>> GetAnalysesAsync(
        int page, int? pageSize, bool withProgress, bool queued, CancellationToken cancellationToken
    )
    {
        return infoRepository.GetAnalysisSnapshotsAsync(
            page,
            ValidatePageSize(pageSize),
            withProgress,
            queued,
            permissionService.WhereCanReadAnalysisAsync,
            cancellationToken
        );
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisAsync(Guid executionId, bool withProgress, bool checkPermission, CancellationToken cancellationToken)
    {
        AnalysisContextSnapshot? snapshot = await infoRepository.GetAnalysisSnapshotAsync(executionId, withProgress, cancellationToken);

        if (checkPermission && snapshot is not null)
        {
            await permissionService.CheckCanReadAnalysisAsync(snapshot.PermissionAssignments, cancellationToken);
        }

        return snapshot;
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisAsync(AnalysisCoord analysisCoord, bool withProgress, bool checkPermission, CancellationToken cancellationToken)
    {
        AnalysisContextSnapshot? snapshot = await infoRepository.GetAnalysisSnapshotAsync(analysisCoord, withProgress, cancellationToken);

        if (checkPermission && snapshot is not null)
        {
            await permissionService.CheckCanReadAnalysisAsync(snapshot.PermissionAssignments, cancellationToken);
        }

        return snapshot;
    }

    public IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysesAE(Guid analysisId, bool withProgress, CancellationToken cancellationToken)
    {
        return infoRepository.GetAnalysisSnapshotsAE(analysisId, withProgress, permissionService.WhereCanReadAnalysisAsync, cancellationToken);
    }

    private int ValidatePageSize(int? pageSize)
    {
        int maxPageSize = coreOptions.MaxPageSize;
        if (pageSize > maxPageSize)
        {
            throw AnalysisExceptions.InputGreaterThan(nameof(pageSize), maxPageSize);
        }

        return pageSize ?? coreOptions.DefaultPageSize;
    }
}
