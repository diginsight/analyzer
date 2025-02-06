using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using System.Net;

namespace Diginsight.Analyzer.API.Services;

internal sealed class OrchestratorWaitingService : IWaitingService
{
    public Task<AnalysisContextSnapshot> WaitAsync(Guid executionId, CancellationToken cancellationToken)
    {
        throw new AnalysisException("`wait` not implemented yet in orchestrator", HttpStatusCode.NotImplemented, "WaitNotImplementedYet");
    }
}
