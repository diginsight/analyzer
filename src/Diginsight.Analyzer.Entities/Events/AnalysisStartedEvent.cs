namespace Diginsight.Analyzer.Entities.Events;

public sealed class AnalysisStartedEvent : Event, IAnalysisEvent, IStartedEvent
{
    public override EventKind EventKind => EventKind.AnalysisStarted;

    public required AnalysisCoord AnalysisCoord { get; init; }

    public required bool Queued { get; init; }
}
