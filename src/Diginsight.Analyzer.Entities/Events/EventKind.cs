using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities.Events;

[JsonConverter(typeof(StringEnumConverter))]
public enum EventKind
{
    AnalysisStarted,
    AnalysisFinished,
    StepStarted,
    StepFinished,
    StepCustom,
}
