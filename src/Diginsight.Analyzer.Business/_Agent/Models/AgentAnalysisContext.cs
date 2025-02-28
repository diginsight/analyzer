using Diginsight.Analyzer.Entities.Permissions;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal sealed class AgentAnalysisContext : ExecutionContext, IAgentAnalysisContext
{
    private readonly IReadOnlyList<StepHistory> steps;
    private readonly IReadOnlyDictionary<string, int> stepIndexes;

    public AnalysisCoord AnalysisCoord { get; }

    public GlobalMeta GlobalMeta { get; }

    public IEnumerable<StepHistory> Steps => steps;

    IEnumerable<IStepHistoryRO> IAnalysisContextRO.Steps => Steps;

    public JObject Progress { get; }

    JObject IAnalysisContextRO.ProgressRO => new (Progress);

    public DateTime? QueuedAt { get; }

    public DateTime? StartedAt { get; }

    public DateTime? FinishedAt { get; set; }

    public string? AgentName { get; }

    public string AgentPool { get; }

    public TimeBoundStatus Status { get; set; }

    public IEnumerable<ISpecificPermissionAssignment<AnalysisPermission>> PermissionAssignments { get; }

    public AgentAnalysisContext(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        DateTime? queuedAt,
        string agentPool,
        DateTime startedAt,
        string agentName,
        Guid principalId
    )
        : base(new ExecutionCoord(ExecutionKind.Analysis, executionId))
    {
        AnalysisCoord = analysisCoord;
        GlobalMeta = globalMeta;
        this.steps = steps.Select(static x => x as StepHistory ?? new StepHistory(x)).ToArray();
        Progress = progress;
        stepIndexes = this.steps
            .Select(static (x, i) => (x.Meta.InternalName, Index: i))
            .ToDictionary(static x => x.InternalName, static x => x.Index);
        QueuedAt = queuedAt;
        AgentPool = agentPool;
        StartedAt = startedAt;
        AgentName = agentName;
        PermissionAssignments = [ new AnalysisSpecificPermissionAssignment(AnalysisPermission.Invoke, principalId) ];
        Status = TimeBoundStatus.Running;
    }

    public StepHistory GetStep(string internalName) => steps[stepIndexes[internalName]];

    IStepHistoryRO IAnalysisContextRO.GetStep(string internalName) => GetStep(internalName);
}
