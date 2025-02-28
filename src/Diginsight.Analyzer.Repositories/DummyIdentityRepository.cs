namespace Diginsight.Analyzer.Repositories;

internal sealed class DummyIdentityRepository : IIdentityRepository
{
    public (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal() => (Guid.Empty, null);

    public ValueTask<IEnumerable<Guid>> GetPrincipalIdsAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Enumerable.Empty<Guid>());
}
