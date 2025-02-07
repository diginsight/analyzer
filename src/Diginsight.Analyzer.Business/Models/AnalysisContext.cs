using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal sealed class AnalysisContext : ExecutionContext, IAgentAnalysisContext
{
    private readonly IReadOnlyList<StepHistory> steps;
    private readonly IReadOnlyDictionary<string, int> stepIndexes;

    [JsonProperty]
    private readonly DateTime? startedAt;

    // ReSharper disable once ReplaceWithFieldKeyword
    [JsonProperty]
    private readonly string? agentName;

    public AnalysisCoord AnalysisCoord { get; }

    public GlobalMeta GlobalMeta { get; }

    public IEnumerable<StepHistory> Steps => steps;

    IEnumerable<IStepHistoryRO> IAnalysisContextRO.Steps => Steps;

    public JObject Progress { get; }

    JObject IAnalysisContextRO.ProgressRO => new (Progress);

    public DateTime? QueuedAt { get; }

    [JsonIgnore]
    public DateTime StartedAt => startedAt ?? throw new InvalidOperationException("Not started");

    public DateTime? FinishedAt { get; set; }

    [JsonIgnore]
    public string AgentName => agentName ?? throw new InvalidOperationException("Not started");

    public string AgentPool { get; }

    public TimeBoundStatus Status { get; set; }

    public AnalysisContext(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        DateTime? queuedAt,
        string agentPool,
        DateTime? startedAt = null,
        string? agentName = null
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
        this.startedAt = startedAt;
        this.agentName = agentName;
    }

    public StepHistory GetStep(string internalName) => steps[stepIndexes[internalName]];

    IStepHistoryRO IAnalysisContextRO.GetStep(string internalName) => GetStep(internalName);

    public override bool IsNotStarted() => startedAt is null;
}
