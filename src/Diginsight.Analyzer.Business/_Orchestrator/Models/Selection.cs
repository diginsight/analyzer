using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Business.Models;

[Flags]
[JsonConverter(typeof(StringEnumConverter))]
public enum Selection
{
    Running = 1 << 0,
    Queued = 1 << 1,
    All = Running | Queued,
}
