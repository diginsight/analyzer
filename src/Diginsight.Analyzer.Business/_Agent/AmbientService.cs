namespace Diginsight.Analyzer.Business;

internal sealed class AmbientService : IAmbientService
{
    private readonly TimeProvider timeProvider;

    public AmbientService(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public Guid NewUlid() => Ulid.NewUlid(timeProvider.GetUtcNow()).ToGuid();
}
