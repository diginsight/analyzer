using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Repositories.Models;

public sealed class AnalysisContextSnapshot : ExecutionContextSnapshot
{
    public Guid AnalysisId { get; }

    public int Attempt { get; }

    [JsonIgnore]
    public AnalysisCoord AnalysisCoord { get; }

    public string? AgentName { get; init; }

    public string AgentPool { get; init; } = null!;

    public DateTime? QueuedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public GlobalMeta GlobalMeta { get; init; } = null!;

    public IEnumerable<StepHistory> Steps { get; init; } = null!;

    [JsonProperty("status")]
    public TimeBoundStatus Status { get; init; }

    [DisallowNull]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Progress { get; set; }

    [JsonConstructor]
    internal AnalysisContextSnapshot(Guid id, Guid analysisId, int attempt)
        : base(id)
    {
        AnalysisId = analysisId;
        Attempt = attempt;
        AnalysisCoord = new AnalysisCoord(analysisId, attempt);
    }
}
