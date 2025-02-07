using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContextRO : IExecutionContextRO, ITimeBoundRO
{
    AnalysisCoord AnalysisCoord { get; }

    string AgentName { get; }

    string AgentPool { get; }

    DateTime? QueuedAt { get; }

    DateTime StartedAt { get; }

    GlobalMeta GlobalMeta { get; }

    IEnumerable<IStepHistoryRO> Steps { get; }

    JObject ProgressRO { get; }

    IStepHistoryRO GetStep(string internalName);
}
