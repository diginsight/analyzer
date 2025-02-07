using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Diginsight.Analyzer.Entities;

public interface IStepInstance
{
    StepMeta Meta { get; }
    JObject Input { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed void Deconstruct(out StepMeta meta, out JObject input)
    {
        meta = Meta;
        input = Input;
    }
}
