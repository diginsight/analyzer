using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal interface IInternalAnalysisService
{
    Task<IEnumerable<AnalyzerStepWithInput>> CalculateStepsAsync(IEnumerable<StepInstance> steps, CancellationToken cancellationToken);

    void FillLease(AnalysisLease lease, AnalysisCoord coord);

    Task<bool> HasConflictAsync(ActiveLease lease, IEnumerable<AnalyzerStepWithInput> analyzerStepsWithInput, CancellationToken cancellationToken);
}
