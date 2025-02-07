namespace Diginsight.Analyzer.Entities;

public interface ITimeBoundRO
{
    DateTime? FinishedAt { get; }

    TimeBoundStatus Status { get; }
}
