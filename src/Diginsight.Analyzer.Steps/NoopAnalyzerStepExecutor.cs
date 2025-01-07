using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    public StepMeta Meta { get; }
    public JObject Input { get; }

    public NoopAnalyzerStepExecutor(StepMeta meta, JObject input)
    {
        Meta = meta;
        Input = input;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
