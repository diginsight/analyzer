using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class DelayAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    private readonly DelayAnalyzerStepInput.Final input;

    public StepMeta Meta { get; }
    public JObject Input { get; }
    public IStepCondition Condition { get; }

    public DelayAnalyzerStepExecutor(StepMeta meta, JObject input, IStepCondition condition)
    {
        Meta = meta;
        Input = input;
        Condition = condition;

        this.input = input.ToObject<DelayAnalyzerStepInput.Final>()!;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.Delay(input.Delay, cancellationToken);
    }
}
