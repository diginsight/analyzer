using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Steps;

internal sealed class DelayAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    private readonly DelayAnalyzerStepInput.Validated input;

    public StepMeta Meta { get; }
    public JObject RawInput { get; }

    public object ValidatedInput
    {
        get => input;
        [MemberNotNull(nameof(input))]
        private init => input = (DelayAnalyzerStepInput.Validated)value;
    }

    public IStepCondition Condition { get; }

    public DelayAnalyzerStepExecutor(StepMeta meta, AnalyzerStepExecutorInputs inputs)
    {
        Meta = meta;
        (RawInput, ValidatedInput, Condition) = inputs;
    }

    public Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        return Task.Delay(input.Delay, cancellationToken);
    }
}
