using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IOrchestratorAnalysisContextFactory
{
    IAnalysisContextRO Make(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        string agentPool
    );
}
