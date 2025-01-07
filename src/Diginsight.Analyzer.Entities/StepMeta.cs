using Newtonsoft.Json;
using System.ComponentModel;

namespace Diginsight.Analyzer.Entities;

public sealed class StepMeta
{
    // ReSharper disable once ReplaceWithFieldKeyword
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    private readonly string? displayName;

    public string Template { get; }

    public string InternalName { get; }

    [JsonIgnore]
    public string DisplayName => displayName ?? InternalName;

    public string? Condition { get; }

    public IEnumerable<string> DependsOn { get; }

    [JsonConstructor]
    public StepMeta(string template, string internalName, string? displayName = null, string? condition = null, IEnumerable<string>? dependsOn = null)
    {
        Template = template;
        InternalName = internalName;
        this.displayName = displayName;
        Condition = condition;
        DependsOn = dependsOn ?? [ ];
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out string template, out string internalName)
    {
        template = Template;
        internalName = InternalName;
    }
}
