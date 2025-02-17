using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public interface IPermission<TPermission> : IEquatable<TPermission>
    where TPermission : IPermission<TPermission>
{
    static abstract IReadOnlyDictionary<string, TPermission> Values { get; }

    string Name { get; }

    bool IEquatable<TPermission>.Equals(TPermission? other) => Name == other?.Name;

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
