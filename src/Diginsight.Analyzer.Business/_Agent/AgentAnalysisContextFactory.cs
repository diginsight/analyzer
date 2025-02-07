using Diginsight.Analyzer.Business.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentAnalysisContextFactory : IAgentAnalysisContextFactory
{
    private readonly IAgentAmbientService ambientService;
    private readonly TimeProvider timeProvider;

    public AgentAnalysisContextFactory(
        IAgentAmbientService ambientService,
        TimeProvider timeProvider
    )
    {
        this.ambientService = ambientService;
        this.timeProvider = timeProvider;
    }

    public IAgentAnalysisContext Make(
        Guid executionId,
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<StepInstance> steps,
        JObject progress,
        DateTime? queuedAt
    )
    {
        return new AnalysisContext(
            executionId,
            coord,
            globalMeta,
            steps,
            progress,
            queuedAt,
            ambientService.AgentPool,
            timeProvider.GetUtcNow().UtcDateTime,
            ambientService.AgentName
        )
        {
            Status = TimeBoundStatus.Running,
        };
    }
}
