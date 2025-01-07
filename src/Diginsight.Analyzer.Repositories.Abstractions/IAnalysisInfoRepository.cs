using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Repositories;

public interface IAnalysisInfoRepository
{
    Task InsertAsync(IAnalysisContext analysisContext);

    Task UpsertAsync(IAnalysisContext analysisContext);

    Task DeleteAsync(Guid executionId);

    IDisposable? StartTimedProgressFlush(IAnalysisContext analysisContext);

    Task<Page<AnalysisContextSnapshot>> GetAnalysisSnapshotsAsync(int page, int pageSize, bool withProgress, bool queued, CancellationToken cancellationToken);

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(Guid executionId, bool withProgress, CancellationToken cancellationToken);

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoord analysisCoord, bool withProgress, CancellationToken cancellationToken);

    IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysisSnapshotsAE(Guid analysisId, bool withProgress, CancellationToken cancellationToken);

    IAsyncEnumerable<AnalysisContextSnapshot> GetAllQueuedAnalysisSnapshotsAE(CancellationToken cancellationToken);
}
