using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    public StepMeta Meta { get; }
    public JObject RawInput { get; }
    public object ValidatedInput => NoopAnalyzerStep.ValidatedInput;
    public IStepCondition Condition { get; }

    public NoopAnalyzerStepExecutor(StepMeta meta, JObject rawInput, IStepCondition condition)
    {
        Meta = meta;
        RawInput = rawInput;
        Condition = condition;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
