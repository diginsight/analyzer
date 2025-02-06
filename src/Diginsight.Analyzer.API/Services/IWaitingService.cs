using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.API.Services;

public interface IWaitingService
{
    Task<AnalysisContextSnapshot> WaitAsync(Guid executionId, CancellationToken cancellationToken);
}
