namespace Diginsight.Analyzer.Entities.Events;

public sealed class StepStartedEvent : StepEvent
{
    public override EventKind EventKind => EventKind.StepStarted;
}
