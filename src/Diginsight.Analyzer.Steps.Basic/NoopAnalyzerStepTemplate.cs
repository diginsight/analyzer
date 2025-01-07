using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Steps;

public sealed class NoopAnalyzerStepTemplate : IAnalyzerStepTemplate
{
    public string Name => "Noop";

    public IAnalyzerStep Create(StepMeta meta) => new NoopAnalyzerStep(this, meta);
}
