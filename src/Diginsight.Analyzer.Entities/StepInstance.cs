using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Diginsight.Analyzer.Entities;

public class StepInstance
{
    public StepMeta Meta { get; }
    public JObject Input { get; }

    [JsonConstructor]
    public StepInstance(StepMeta meta, JObject input)
    {
        Meta = meta;
        Input = input;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out StepMeta meta, out JObject input)
    {
        meta = Meta;
        input = Input;
    }
}
