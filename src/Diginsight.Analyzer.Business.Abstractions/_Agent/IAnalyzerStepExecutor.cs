using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IAnalyzerStepExecutor : IStepInstance
{
    JObject RawInput { get; }

    JObject IStepInstance.Input => RawInput;

    object ValidatedInput { get; }

    IStepCondition Condition { get; }

    bool DisableProgressFlushTimer => false;

    Task SetupAsync(IAnalysisContext analysisContext, IStepHistory stepHistory, CancellationToken cancellationToken) => Task.CompletedTask;

    Task ExecuteAsync(IAnalysisContext analysisContext, IStepHistory stepHistory, CancellationToken cancellationToken);

    Task TeardownAsync(IAnalysisContext analysisContext, IStepHistory stepHistory, CancellationToken cancellationToken) => Task.CompletedTask;
}
