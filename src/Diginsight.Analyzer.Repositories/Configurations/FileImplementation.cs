using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Repositories.Configurations;

[JsonConverter(typeof(StringEnumConverter))]
internal enum FileImplementation
{
    Blob,
    Physical,
}
