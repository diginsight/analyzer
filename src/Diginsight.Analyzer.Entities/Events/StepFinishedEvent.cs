namespace Diginsight.Analyzer.Entities.Events;

public sealed class StepFinishedEvent : StepEvent, IFinishedEvent
{
    public override EventKind EventKind => EventKind.StepFinished;

    public required FinishedEventStatus Status { get; init; }
}
