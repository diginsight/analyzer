namespace Diginsight.Analyzer.Entities.Events;

public interface IFinishedEvent
{
    FinishedEventStatus Status { get; }
}
