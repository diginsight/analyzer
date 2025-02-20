namespace Diginsight.Analyzer.Repositories;

public interface IIdentityRepository
{
    (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal();

    Task<IEnumerable<Guid>> GetGroupIdsAsync(Guid objectId, bool isUser, CancellationToken cancellationToken);
}
