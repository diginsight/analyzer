using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class Lease : Expandable<Lease>
{
    [JsonProperty("id")]
    [field: MaybeNull]
    public string Id
    {
        get => field ?? throw new InvalidOperationException($"{nameof(Id)} is unset");
        init;
    }

    public Uri BaseAddress { get; init; } = null!;

    public string AgentName { get; init; } = null!;

    public string AgentPool { get; init; } = null!;

    [JsonProperty("ttl")]
    public int TtlSeconds { get; init; }

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public ExecutionKind? Kind { get; set; }

    [JsonConstructor]
    public Lease() { }

    public Lease(Lease other)
    {
        Id = other.Id;
        BaseAddress = other.BaseAddress;
        AgentName = other.AgentName;
        AgentPool = other.AgentPool;
        TtlSeconds = other.TtlSeconds;
    }

    public virtual ActiveLease? AsActive() => Kind switch
    {
        ExecutionKind.Analysis => As<AnalysisLease>(),
        _ => null,
    };
}
