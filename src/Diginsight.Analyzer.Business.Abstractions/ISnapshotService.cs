using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public interface ISnapshotService
{
    Task<Page<AnalysisContextSnapshot>> GetAnalysesAsync(int page, int? pageSize, bool withProgress, bool queued, CancellationToken cancellationToken);

    Task<AnalysisContextSnapshot?> GetAnalysisAsync(Guid executionId, bool withProgress, bool checkPermission, CancellationToken cancellationToken);

    Task<AnalysisContextSnapshot?> GetAnalysisAsync(AnalysisCoord coord, bool withProgress, bool checkPermission, CancellationToken cancellationToken);

    IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysesAE(Guid analysisId, bool withProgress, CancellationToken cancellationToken);
}
