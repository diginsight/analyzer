using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public sealed class EventRecipient
{
    public string Name { get; }

    public EventRecipientInput Input { get; }

    [JsonConstructor]
    public EventRecipient(string name, EventRecipientInput? input = null)
    {
        Name = name;
        Input = input ?? new EventRecipientInput();
    }
}
