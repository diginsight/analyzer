using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Events;

public class StepCustomEvent : StepEvent
{
    public override EventKind EventKind => EventKind.StepCustom;

    [JsonConstructor]
    protected StepCustomEvent() { }
}
