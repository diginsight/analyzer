using Diginsight.Analyzer.Business.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal interface IAgentAnalysisContextFactory
{
    IAgentAnalysisContext Make(
        Guid executionId,
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        DateTime? queuedAt
    );
}
