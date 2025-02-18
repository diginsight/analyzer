namespace Diginsight.Analyzer.Repositories;

public interface IIdentityRepository
{
    Task<IEnumerable<Guid>> GetGroupsAsync(Guid objectId, bool isUser, CancellationToken cancellationToken);
}
