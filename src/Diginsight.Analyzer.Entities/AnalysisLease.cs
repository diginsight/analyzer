namespace Diginsight.Analyzer.Entities;

public sealed class AnalysisLease : ActiveLease
{
    public Guid AnalysisId { get; set; }

    public int Attempt { get; set; }

    public override ActiveLease AsActive() => As<AnalysisLease>();
}
