namespace Diginsight.Analyzer.Entities.Events;

public sealed class AnalysisFinishedEvent : Event, IAnalysisEvent, IFinishedEvent
{
    public override EventKind EventKind => EventKind.AnalysisFinished;

    public required AnalysisCoord AnalysisCoord { get; init; }

    public required FinishedEventStatus Status { get; init; }
}
