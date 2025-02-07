namespace Diginsight.Analyzer.Entities;

public interface IStepHistoryRO : IStepInstance, ITimeBoundWithPhasesRO, ISkippableRO, IFailableRO
{
    new Exception? Reason { get; }

    DateTime? SetupStartedAt { get; }

    DateTime? StartedAt { get; }

    DateTime? TeardownStartedAt { get; }
}
