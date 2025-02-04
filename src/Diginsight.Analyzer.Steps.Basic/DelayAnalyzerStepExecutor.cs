using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class DelayAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    private readonly DelayAnalyzerStepInput.Validated input;

    public StepMeta Meta { get; }
    public JObject RawInput { get; }
    public object ValidatedInput => input;
    public IStepCondition Condition { get; }

    public DelayAnalyzerStepExecutor(StepMeta meta, JObject rawInput, DelayAnalyzerStepInput.Validated validatedInput, IStepCondition condition)
    {
        Meta = meta;
        RawInput = rawInput;
        input = validatedInput;
        Condition = condition;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.Delay(input.Delay, cancellationToken);
    }
}
