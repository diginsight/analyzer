namespace Diginsight.Analyzer.Entities;

public interface ITimeBoundWithPhasesRO : ITimeBoundRO
{
    DateTime? SetupFinishedAt { get; }

    DateTime? TeardownFinishedAt { get; }
}
