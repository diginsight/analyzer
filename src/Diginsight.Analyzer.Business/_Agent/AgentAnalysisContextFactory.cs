using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentAnalysisContextFactory : IAgentAnalysisContextFactory
{
    private readonly IAgentAmbientService ambientService;
    private readonly IIdentityRepository identityRepository;
    private readonly TimeProvider timeProvider;

    public AgentAnalysisContextFactory(
        IAgentAmbientService ambientService,
        IIdentityRepository identityRepository,
        TimeProvider timeProvider
    )
    {
        this.ambientService = ambientService;
        this.identityRepository = identityRepository;
        this.timeProvider = timeProvider;
    }

    public IAgentAnalysisContext Make(
        Guid executionId,
        AnalysisCoord coord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        DateTime? queuedAt
    )
    {
        return new AgentAnalysisContext(
            executionId,
            coord,
            globalMeta,
            steps,
            progress,
            queuedAt,
            ambientService.AgentPool,
            timeProvider.GetUtcNow().UtcDateTime,
            ambientService.AgentName,
            identityRepository.GetMainPrincipal().ObjectId
        )
        {
            Status = TimeBoundStatus.Running,
        };
    }
}
