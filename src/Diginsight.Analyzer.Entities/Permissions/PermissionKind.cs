using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities.Permissions;

[JsonConverter(typeof(StringEnumConverter))]
public enum PermissionKind
{
    Analysis,
    Permission,
    Plugin,
}
