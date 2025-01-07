#if FEATURE_REPORTS
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Immutable;

namespace Diginsight.Analyzer.Business.Models;

public class StepReport
{
    public string InternalName { get; }

    public TimeBoundStatus Status { get; }

    [JsonConverter(typeof(ProblemsConverter))]
    public IReadOnlyDictionary<object, Problem> Problems { get; }

    public StepReport(string internalName, TimeBoundStatus status, IReadOnlyDictionary<object, Problem>? problems = null)
    {
        InternalName = internalName;
        Status = status;
        Problems = problems ?? ImmutableDictionary<object, Problem>.Empty;
    }
}

file class ProblemsConverter : JsonConverter
{
    public override bool CanRead => false;

    public override bool CanWrite => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            serializer.Serialize(writer, value);
            return;
        }

        IContractResolver contractResolver = serializer.ContractResolver;

        IReadOnlyDictionary<object, Problem> dict = (IReadOnlyDictionary<object, Problem>)value;
        bool allPrimitives = dict.Keys.All(
            k =>
            {
                Type kt = k.GetType();
                return kt == typeof(JValue) || contractResolver.ResolveContract(kt) is JsonPrimitiveContract;
            }
        );
        serializer.Serialize(writer, allPrimitives ? dict : dict.ToArray());
    }

    public override bool CanConvert(Type objectType)
    {
        throw new NotSupportedException();
    }
}
#endif
