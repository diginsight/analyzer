using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public sealed class EventRecipient
{
    public string Name { get; }

    public JObject Input { get; }

    [JsonConstructor]
    public EventRecipient(string name, JObject? input = null)
    {
        Name = name;
        Input = input ?? new JObject();
    }
}
