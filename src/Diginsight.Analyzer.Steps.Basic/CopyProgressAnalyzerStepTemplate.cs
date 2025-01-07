using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Steps;

public sealed class CopyProgressAnalyzerStepTemplate : IAnalyzerStepTemplate
{
    public string Name => "CopyProgress";

    public IAnalyzerStep Create(StepMeta meta) => new CopyProgressAnalyzerStep(this, meta);
}
