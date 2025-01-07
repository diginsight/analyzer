using Diginsight.Analyzer.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Runtime.Serialization;

namespace Diginsight.Analyzer.Entities;

public abstract class Expandable<T> : IExpandable<T>
    where T : Expandable<T>, new()
{
    [JsonExtensionData]
    private readonly JObject additionalData = new ();

    [JsonIgnore]
    private readonly ISet<Type> seenTypes;

    [JsonIgnore]
    private bool expanding = false;

    protected Expandable()
    {
        seenTypes = new HashSet<Type>() { GetType() };
    }

    public TExpansion As<TExpansion>()
        where TExpansion : T, new()
    {
        if (this is TExpansion expansion)
        {
            return expansion;
        }

        GetSerializerSettingsAndContractResolver(out JsonSerializerSettings serializerSettings, out IContractResolver contractResolver);

        static void ReincludeProperties(JsonObjectContract contract)
        {
            ForEachIgnoredProperty(contract, static x => { x.Ignored = false; });
        }

        ReincludeProperties((JsonObjectContract)contractResolver.ResolveContract(GetType()));
        ReincludeProperties((JsonObjectContract)contractResolver.ResolveContract(typeof(TExpansion)));

        JsonSerializer serializer = JsonSerializer.CreateDefault(serializerSettings);

        string tempFileName = Path.GetTempFileName();
        try
        {
            using FileStream stream = File.Open(tempFileName, FileMode.Open);
            expanding = true;
            try
            {
                serializer.Serialize(stream, this);
            }
            finally
            {
                expanding = false;
            }

            stream.Position = 0;

            expansion = serializer.Deserialize<TExpansion>(stream);
            foreach (Type seenType in seenTypes)
            {
                expansion.seenTypes.Add(seenType);
            }
        }
        finally
        {
            File.Delete(tempFileName);
        }

        return expansion;
    }

    private static void GetSerializerSettingsAndContractResolver(out JsonSerializerSettings serializerSettings, out IContractResolver contractResolver)
    {
        serializerSettings = JsonConvert.DefaultSettings!();
        contractResolver = serializerSettings.ContractResolver!;
    }

    private static void ForEachIgnoredProperty(JsonObjectContract contract, Action<JsonProperty> action)
    {
        foreach (JsonProperty property in contract.Properties.Where(static x => x.Ignored && x.DeclaringType != typeof(Expandable<>)))
        {
            action(property);
        }
    }

    [OnSerializing]
    private void OnSerializing(StreamingContext streamingContext)
    {
        if (expanding)
        {
            return;
        }

        GetSerializerSettingsAndContractResolver(out _, out IContractResolver contractResolver);

        foreach (JsonObjectContract contract in seenTypes.Select(x => (JsonObjectContract)contractResolver.ResolveContract(x)))
        {
            ForEachIgnoredProperty(contract, x => additionalData.Remove(x.PropertyName!));
        }
    }
}
