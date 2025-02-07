namespace Diginsight.Analyzer.API.Services;

public interface IWaitingService
{
    Task WaitAsync(Guid executionId, CancellationToken cancellationToken);
}
