#if FEATURE_REPORTS
using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

public sealed class AnalysisReport
{
    public required IEnumerable<StepReport> Steps { get; init; }
}
#endif
