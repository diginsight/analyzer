using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContextRO : IExecutionContextRO, ITimeBound
{
    AnalysisCoord AnalysisCoord { get; }

    string AgentName { get; }

    string AgentPool { get; }

    DateTime? QueuedAt { get; }

    DateTime StartedAt { get; }

    GlobalMeta GlobalMeta { get; }

    IEnumerable<IStepHistoryRO> Steps { get; }

    JObject Progress { get; }

    IStepHistoryRO GetStep(string internalName);
}
