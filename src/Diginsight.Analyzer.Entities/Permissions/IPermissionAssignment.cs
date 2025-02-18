using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Diginsight.Analyzer.Entities.Permissions;

[JsonConverter(typeof(Converter))]
public interface IPermissionAssignment
{
    PermissionKind Kind { get; }

    [JsonProperty("principalId")]
    sealed Guid? PrincipalId => throw new NotSupportedException();

    bool NeedsEnabler();

    bool IsEnabledBy(IEnumerable<IPermissionAssignmentEnabler> enablers);

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
    TPermission Permission { get; }

    bool IPermissionAssignment.IsEnabledBy(IEnumerable<IPermissionAssignmentEnabler> enablers)
    {
        return !NeedsEnabler() || enablers.Any(x => x is IPermissionAssignmentEnabler<TPermission> enabler0 && enabler0.Permission >> Permission);
    }
}

public interface IPermissionAssignment<out TPermission, TSubject> : IPermissionAssignment<TPermission>
    where TPermission : struct, IPermission<TPermission>
    where TSubject : struct, IEquatable<TSubject>
{
    TSubject? SubjectId { get; }

    public static bool operator >> (IPermissionAssignment<TPermission, TSubject> left, IPermissionAssignment right)
    {
        if (right is not IPermissionAssignment<TPermission, TSubject> other)
            return false;

        switch (left.SubjectId, other.SubjectId)
        {
            case (null, null):
            case ({ } subjectId, { } otherSubjectId) when subjectId.Equals(otherSubjectId):
                return left.Permission >> other.Permission;

            case (null, not null):
                return left.Permission == other.Permission;

            default:
                return false;
        }
    }
}
