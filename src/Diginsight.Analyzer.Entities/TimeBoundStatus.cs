using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(StringEnumConverter))]
public enum TimeBoundStatus
{
    Pending,
    Running,
    PartiallyCompleted,
    Completed,
    Aborting,
    Aborted,
}
