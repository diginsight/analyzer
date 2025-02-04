using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Steps;

public sealed class DelayAnalyzerStepTemplate : IAnalyzerStepTemplate
{
    public string Name => "Delay";

    public IAnalyzerStep Create(StepMeta meta) => new DelayAnalyzerStep(this, meta);
}
