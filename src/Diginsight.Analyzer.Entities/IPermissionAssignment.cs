using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(Converter))]
public interface IPermissionAssignment
{
    PermissionSubjectKind Kind { get; }
    object Permission { get; }
    object? SubjectId { get; }
    Guid? PrincipalId { get; }

    private sealed class Converter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(IPermissionAssignment);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject raw = serializer.Deserialize<JObject>(reader)!;
            return raw.GetValue(nameof(Kind), StringComparison.OrdinalIgnoreCase)!.ToObject<PermissionSubjectKind>() switch
            {
                PermissionSubjectKind.Analysis => raw.ToObject<AnalysisPermissionAssignment>(serializer),
                PermissionSubjectKind.Permission => raw.ToObject<PermissionPermissionAssignment>(serializer),
                _ => throw new UnreachableException($"Unrecognized {nameof(PermissionSubjectKind)}"),
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
