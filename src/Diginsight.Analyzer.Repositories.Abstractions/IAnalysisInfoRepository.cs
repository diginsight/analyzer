﻿using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Repositories;

public interface IAnalysisInfoRepository
{
    Task InsertAsync(IAnalysisContextRO analysisContext);

    Task UpsertAsync(IAnalysisContextRO analysisContext);

    Task DeleteAsync(Guid executionId);

    IDisposable? StartTimedProgressFlush(IAnalysisContextRO analysisContext);

    Task<Page<AnalysisContextSnapshot>> GetAnalysisSnapshotsAsync(
        int page,
        int pageSize,
        bool withProgress,
        bool queued,
        Func<IQueryable<AnalysisContextSnapshot>, CancellationToken, Task<IQueryable<AnalysisContextSnapshot>>> whereCanReadAsync,
        CancellationToken cancellationToken
    );

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(Guid executionId, bool withProgress, CancellationToken cancellationToken);

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoord analysisCoord, bool withProgress, CancellationToken cancellationToken);

    IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysisSnapshotsAE(
        Guid analysisId,
        bool withProgress,
        Func<IQueryable<AnalysisContextSnapshot>, CancellationToken, Task<IQueryable<AnalysisContextSnapshot>>> whereCanReadAsync,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<AnalysisContextSnapshot> GetAllQueuedAnalysisSnapshotsAE(CancellationToken cancellationToken);
}
