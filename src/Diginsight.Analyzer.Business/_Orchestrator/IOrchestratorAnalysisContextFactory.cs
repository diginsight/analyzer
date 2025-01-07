using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IOrchestratorAnalysisContextFactory
{
    IAnalysisContext Make(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<StepInstance> steps,
        JObject progress,
        string agentPool
    );
}
