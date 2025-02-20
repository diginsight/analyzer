namespace Diginsight.Analyzer.Repositories;

public interface IIdentityRepository
{
    Task<IEnumerable<Guid>> GetGroupIdsAsync(Guid objectId, bool isUser, CancellationToken cancellationToken);
}
