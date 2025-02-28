using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorAnalysisContextFactory : IOrchestratorAnalysisContextFactory
{
    private readonly IIdentityRepository identityRepository;
    private readonly TimeProvider timeProvider;

    public OrchestratorAnalysisContextFactory(
        IIdentityRepository identityRepository,
        TimeProvider timeProvider
    )
    {
        this.identityRepository = identityRepository;
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
        return new OrchestratorAnalysisContext(
            executionId, analysisCoord, globalMeta, steps, progress, timeProvider.GetUtcNow().UtcDateTime, agentPool, identityRepository.GetMainPrincipal().ObjectId
        );
    }
}
