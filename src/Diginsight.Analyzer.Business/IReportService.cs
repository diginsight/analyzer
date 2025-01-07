#if FEATURE_REPORTS
namespace Diginsight.Analyzer.Business;

public interface IReportService
{
    Task<AnalysisReport?> GetReportAsync(Guid executionId, CancellationToken cancellationToken);

    Task<AnalysisReport?> GetReportAsync(AnalysisCoord coord, CancellationToken cancellationToken);
}
#endif
