using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Common;

[JsonConverter(typeof(StringEnumConverter))]
public enum DependencyExceptionKind
{
    UnknownObject,
    UnknownObjectDependencies,
    CircularDependency,
}
