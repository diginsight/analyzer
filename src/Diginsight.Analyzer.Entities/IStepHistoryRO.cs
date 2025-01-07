using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public interface IStepHistoryRO : ITimeBoundWithPhases, ISkippableRO, IFailableRO
{
    new Exception? Reason { get; }

    StepMeta Meta { get; }

    JObject Input { get; }

    DateTime? SetupStartedAt { get; }

    DateTime? StartedAt { get; }

    DateTime? TeardownStartedAt { get; }
}
