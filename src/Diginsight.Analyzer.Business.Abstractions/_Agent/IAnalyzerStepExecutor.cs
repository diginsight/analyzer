using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IAnalyzerStepExecutor
{
    StepMeta Meta { get; }

    JObject Input { get; }

    IStepCondition Condition { get; }

    bool DisableProgressFlushTimer => false;

    Task SetupAsync(IAnalysisContextRO analysisContext, CancellationToken cancellationToken) => Task.CompletedTask;

    Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken);

    Task TeardownAsync(IAnalysisContextRO analysisContext, CancellationToken cancellationToken) => Task.CompletedTask;
}
