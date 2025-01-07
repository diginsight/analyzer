using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public sealed class GlobalMeta
{
    public int? Parallelism { get; }

    public IEnumerable<string>? OutputPayloads { get; }

    public IEnumerable<EventRecipient>? EventRecipients { get; }

    [JsonConstructor]
    public GlobalMeta(
        int? parallelism = null,
        IEnumerable<string>? outputPayloads = null,
        IEnumerable<EventRecipient>? eventRecipients = null
    )
    {
        Parallelism = parallelism;
        OutputPayloads = outputPayloads;
        EventRecipients = eventRecipients;
    }

    public GlobalMeta WithOverwrite(GlobalMeta? other)
    {
        return other is null
            ? this
            : new GlobalMeta(other.Parallelism ?? Parallelism, OutputPayloads, other.EventRecipients ?? EventRecipients);
    }
}
