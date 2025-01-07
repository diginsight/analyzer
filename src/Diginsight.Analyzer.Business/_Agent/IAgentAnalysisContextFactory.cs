using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IAgentAnalysisContextFactory
{
    IAnalysisContext Make(
        Guid executionId,
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<StepInstance> steps,
        JObject progress,
        DateTime? queuedAt
    );
}
