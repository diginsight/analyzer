using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities.Events;

[JsonConverter(typeof(StringEnumConverter))]
public enum FinishedEventStatus
{
    Completed,
    Skipped,
    Failed,
    Aborted,
}
