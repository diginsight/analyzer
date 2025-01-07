using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed class SnapshotService : ISnapshotService
{
    private readonly IAnalysisInfoRepository infoRepository;
    private readonly ICoreOptions coreOptions;

    public SnapshotService(
        IAnalysisInfoRepository infoRepository,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.infoRepository = infoRepository;
        this.coreOptions = coreOptions.Value;
    }

    public Task<Page<AnalysisContextSnapshot>> GetAnalysesAsync(int page, int? pageSize, bool withProgress, bool queued, CancellationToken cancellationToken)
    {
        return infoRepository.GetAnalysisSnapshotsAsync(page, ValidatePageSize(pageSize), withProgress, queued, cancellationToken);
    }

    public Task<AnalysisContextSnapshot?> GetAnalysisAsync(Guid executionId, bool withProgress, CancellationToken cancellationToken)
    {
        return infoRepository.GetAnalysisSnapshotAsync(executionId, withProgress, cancellationToken);
    }

    public Task<AnalysisContextSnapshot?> GetAnalysisAsync(AnalysisCoord analysisCoord, bool withProgress, CancellationToken cancellationToken)
    {
        return infoRepository.GetAnalysisSnapshotAsync(analysisCoord, withProgress, cancellationToken);
    }

    public IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysesAE(Guid analysisId, bool withProgress, CancellationToken cancellationToken)
    {
        return infoRepository.GetAnalysisSnapshotsAE(analysisId, withProgress, cancellationToken);
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
