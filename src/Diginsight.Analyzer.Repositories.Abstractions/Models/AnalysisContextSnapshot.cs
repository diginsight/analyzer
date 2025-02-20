using Diginsight.Analyzer.Entities.Permissions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Repositories.Models;

public sealed class AnalysisContextSnapshot : ExecutionContextSnapshot
{
    [JsonProperty("analysisId")]
    public Guid AnalysisId { get; }

    [JsonProperty("attempt")]
    public int Attempt { get; }

    [JsonIgnore]
    public AnalysisCoord AnalysisCoord { get; }

    public string? AgentName { get; init; }

    public string AgentPool { get; init; } = null!;

    [JsonProperty("queuedAt")]
    public DateTime? QueuedAt { get; init; }

    [JsonProperty("startedAt")]
    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public GlobalMeta GlobalMeta { get; init; } = null!;

    public IEnumerable<IStepHistoryRO> Steps { get; }

    [JsonProperty("status")]
    public TimeBoundStatus Status { get; init; }

    [DisallowNull]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Progress { get; set; }

    public IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> PermissionAssignments { get; }

    [JsonProperty("permissionAssignments")]
    public IEnumerable<SpecificPermissionAssignmentQO> PermissionAssignmentsQO => throw new NotSupportedException();

    [JsonConstructor]
    internal AnalysisContextSnapshot(
        Guid id,
        Guid analysisId,
        int attempt,
        IEnumerable<StepHistory> steps,
        IEnumerable<AnalysisSpecificPermissionAssignment> permissionAssignments
    )
        : base(id)
    {
        AnalysisId = analysisId;
        Attempt = attempt;
        AnalysisCoord = new AnalysisCoord(analysisId, attempt);
        Steps = steps;
        PermissionAssignments = permissionAssignments;
    }
}
