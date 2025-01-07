#if FEATURE_REPORTS
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(StringEnumConverter))]
public enum ProblemKind
{
    Failed,
    Skipped,
}
#endif
