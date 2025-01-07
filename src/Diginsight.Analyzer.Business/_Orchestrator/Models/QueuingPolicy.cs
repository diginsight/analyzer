using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Business.Models;

[Flags]
[JsonConverter(typeof(StringEnumConverter))]
public enum QueuingPolicy
{
    Never = 0,
    IfFull = 1 << 0,
    IfConflict = 1 << 1,
    IfFullOrConflict = IfFull | IfConflict,
    IfConflictOrFull = IfFullOrConflict,
}
