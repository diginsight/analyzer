using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(Converter))]
public interface IPermissionAssignment
{
    PermissionKind Kind { get; }

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

public interface IPermissionAssignment<out TPermission, out TSubject> : IPermissionAssignment
    where TPermission : IPermission<TPermission>
{
    new TPermission Permission { get; }

    object IPermissionAssignment.Permission => Permission;

    new TSubject? SubjectId { get; }

    object? IPermissionAssignment.SubjectId => SubjectId;
}
