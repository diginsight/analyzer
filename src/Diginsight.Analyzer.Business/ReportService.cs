#if FEATURE_REPORTS
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal sealed class ReportService : IReportService
{
    private readonly ISnapshotService snapshotService;
    private readonly IReadOnlyDictionary<string, IAnalyzerStepTemplate> analyzerStepTemplates;

    public ReportService(
        ISnapshotService snapshotService,
        IEnumerable<IAnalyzerStepTemplate> analyzerStepTemplates
    )
    {
        this.snapshotService = snapshotService;
        this.analyzerStepTemplates = analyzerStepTemplates.ToDictionary(static x => x.Name);
    }

    public async Task<AnalysisReport?> GetReportAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(executionId, true, cancellationToken) is { } snapshot
            ? await GetReportCoreAsync(snapshot, cancellationToken)
            : null;
    }

    public async Task<AnalysisReport?> GetReportAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(coord, true, cancellationToken) is { } snapshot
            ? await GetReportCoreAsync(snapshot, cancellationToken)
            : null;
    }

    private async Task<AnalysisReport?> GetReportCoreAsync(AnalysisContextSnapshot snapshot, CancellationToken cancellationToken)
    {
        JObject progress = snapshot.Progress!;
        return new AnalysisReport()
        {
            Steps = await snapshot.Steps.ToAsyncEnumerable()
                .Select(
                    history =>
                    {
                        StepMeta meta = history.Meta;
                        return analyzerStepTemplates[meta.Template]
                            .Create(meta)
                            .GetReport(history.Status, progress);
                    }
                )
                .ToArrayAsync(cancellationToken),
        };
    }
}
#endif
