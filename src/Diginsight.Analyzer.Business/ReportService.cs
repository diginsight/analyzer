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
        return await snapshotService.GetAnalysisAsync(executionId, true, true, cancellationToken) is { } snapshot
            ? GetReportCore(snapshot)
            : null;
    }

    public async Task<AnalysisReport?> GetReportAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(coord, true, true, cancellationToken) is { } snapshot
            ? GetReportCore(snapshot)
            : null;
    }

    private AnalysisReport GetReportCore(AnalysisContextSnapshot snapshot)
    {
        JObject progress = snapshot.Progress!;
        return new AnalysisReport()
        {
            Steps = snapshot.Steps
                .Select(
                    step =>
                    {
                        StepMeta meta = step.Meta;
                        return analyzerStepTemplates[meta.Template]
                            .Create(meta)
                            .GetReport(step.Status, progress);
                    }
                )
                .ToArray(),
        };
    }
}
#endif
