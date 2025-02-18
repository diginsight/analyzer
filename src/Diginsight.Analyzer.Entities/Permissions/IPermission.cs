using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public interface IPermission
{
    string Name { get; }
}

public interface IPermission<TPermission> : IPermission, IEquatable<TPermission>
    where TPermission : struct, IPermission<TPermission>
{
    static abstract IReadOnlyDictionary<string, TPermission> Values { get; }

    public static abstract bool operator ==(TPermission left, TPermission right);

    public static abstract bool operator !=(TPermission left, TPermission right);

    public static abstract bool operator >>(TPermission left, IPermission right);

    protected sealed class Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(TPermission);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return TPermission.Values[(string?)reader.Value ?? ""];
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(((TPermission)value!).Name);
        }
    }
}
