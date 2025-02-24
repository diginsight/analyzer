namespace Diginsight.Analyzer.Repositories;

public interface IIdentityRepository
{
    (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal();

    ValueTask<IEnumerable<Guid>> GetPrincipalIdsAsync(CancellationToken cancellationToken);
}
