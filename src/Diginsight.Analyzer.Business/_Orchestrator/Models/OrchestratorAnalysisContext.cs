using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal sealed class OrchestratorAnalysisContext : IAnalysisContextRO
{
    private readonly IReadOnlyList<StepHistory> steps;
    private readonly IReadOnlyDictionary<string, int> stepIndexes;

    public ExecutionCoord ExecutionCoord { get; }

    public AnalysisCoord AnalysisCoord { get; }

    public GlobalMeta GlobalMeta { get; }

    public IEnumerable<IStepHistoryRO> Steps => steps;

    public JObject ProgressRO { get; }

    public DateTime? QueuedAt { get; }

    public DateTime? StartedAt => null;

    public DateTime? FinishedAt { get; set; }

    public string? AgentName => null;

    public string AgentPool { get; }

    public TimeBoundStatus Status { get; set; }

    public bool IsFailed => false;

    public Exception? Reason => null;

    public OrchestratorAnalysisContext(
        Guid executionId,
        AnalysisCoord analysisCoord,
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        DateTime queuedAt,
        string agentPool
    )
    {
        ExecutionCoord = new ExecutionCoord(ExecutionKind.Analysis, executionId);
        AnalysisCoord = analysisCoord;
        GlobalMeta = globalMeta;
        this.steps = steps.Select(static x => x as StepHistory ?? new StepHistory(x)).ToArray();
        ProgressRO = progress;
        stepIndexes = this.steps
            .Select(static (x, i) => (x.Meta.InternalName, Index: i))
            .ToDictionary(static x => x.InternalName, static x => x.Index);
        QueuedAt = queuedAt;
        AgentPool = agentPool;
    }

    public StepHistory GetStep(string internalName) => steps[stepIndexes[internalName]];

    IStepHistoryRO IAnalysisContextRO.GetStep(string internalName) => GetStep(internalName);

    public bool IsSucceeded() => true;
}
