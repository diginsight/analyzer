using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public sealed class GlobalMeta
{
    public int? Parallelism { get; }

    public IEnumerable<string>? OutputPayloads { get; }

    public JObject? EventMeta { get; }

    [JsonConstructor]
    public GlobalMeta(
        int? parallelism = null,
        IEnumerable<string>? outputPayloads = null,
        JObject? eventMeta = null
    )
    {
        Parallelism = parallelism;
        OutputPayloads = outputPayloads;
        EventMeta = eventMeta;
    }

    public GlobalMeta WithOverwrite(GlobalMeta? other)
    {
        if (other is null)
            return this;

        JObject finalEventMeta = new ();
        finalEventMeta.Merge(EventMeta);
        finalEventMeta.Merge(other.EventMeta);

        return new GlobalMeta(other.Parallelism ?? Parallelism, OutputPayloads, finalEventMeta);
    }
}
