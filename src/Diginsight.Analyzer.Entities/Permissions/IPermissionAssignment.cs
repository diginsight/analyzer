using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Diginsight.Analyzer.Entities.Permissions;

[JsonConverter(typeof(Converter))]
public interface IPermissionAssignment
{
    PermissionKind Kind { get; }

    [JsonProperty("principalId")]
    Guid? PrincipalId { get; }

    [JsonProperty("permission")]
    string Permission { get; }

    private sealed class Converter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(IPermissionAssignment);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject raw = serializer.Deserialize<JObject>(reader)!;
            return raw.GetValue(nameof(Kind), StringComparison.OrdinalIgnoreCase)!.ToObject<PermissionKind>() switch
            {
                PermissionKind.Analysis => raw.ToObject<AnalysisPermissionAssignment>(serializer),
                PermissionKind.Permission => raw.ToObject<PermissionPermissionAssignment>(serializer),
                _ => throw new UnreachableException($"Unrecognized {nameof(PermissionKind)}"),
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}

public interface IPermissionAssignment<out TPermission> : IPermissionAssignment
    where TPermission : struct, IPermission<TPermission>
{
    new TPermission Permission { get; }

    string IPermissionAssignment.Permission => Permission.Name;
}
