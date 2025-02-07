using Diginsight.Analyzer.Business.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorAnalysisContextFactory : IOrchestratorAnalysisContextFactory
{
    private readonly TimeProvider timeProvider;

    public OrchestratorAnalysisContextFactory(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public IAnalysisContextRO Make(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        string agentPool
    )
    {
        return new AnalysisContext(
            executionId,
            analysisCoord,
            globalMeta,
            steps,
            progress,
            timeProvider.GetUtcNow().UtcDateTime,
            agentPool
        );
    }
}
