using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    public StepMeta Meta { get; }
    public JObject RawInput { get; }
    public object ValidatedInput { get; }
    public IStepCondition Condition { get; }

    public NoopAnalyzerStepExecutor(StepMeta meta, AnalyzerStepExecutorInputs inputs)
    {
        Meta = meta;
        (RawInput, ValidatedInput, Condition) = inputs;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
