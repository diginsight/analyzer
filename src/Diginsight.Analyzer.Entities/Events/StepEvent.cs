namespace Diginsight.Analyzer.Entities.Events;

public abstract class StepEvent : Event
{
    public required AnalysisCoord AnalysisCoord { get; init; }

    public required string Template { get; init; }

    public required string InternalName { get; init; }

    public required Phase Phase { get; init; }
}
