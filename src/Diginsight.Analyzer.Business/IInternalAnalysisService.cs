using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal interface IInternalAnalysisService
{
    Task<IEnumerable<AnalyzerStepExecutorProto2>> CalculateStepsAsync(IEnumerable<IStepInstance> steps, CancellationToken cancellationToken);

    void FillLease(AnalysisLease lease, AnalysisCoord coord);

    Task<bool> HasConflictAsync(ActiveLease lease, IEnumerable<AnalyzerStepExecutorProto1> stepExecutorProtos, CancellationToken cancellationToken);
}
