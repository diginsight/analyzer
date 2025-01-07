using Diginsight.Analyzer.Common;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Repositories;

internal sealed class NewtonsoftJsonCosmosSerializer : CosmosSerializer
{
    public static readonly CosmosSerializer Instance = new NewtonsoftJsonCosmosSerializer();

    private static readonly Encoding Encoding = new UTF8Encoding(false, true);
    private static readonly JsonSerializerSettings SerializerSettings;

    static NewtonsoftJsonCosmosSerializer()
    {
        JsonSerializerSettings serializerSettings = new (JsonConvert.DefaultSettings!());
        serializerSettings.ContractResolver = new CosmosContractResolver(serializerSettings.ContractResolver!);
        serializerSettings.Formatting = Formatting.None;
        SerializerSettings = serializerSettings;
    }

    private NewtonsoftJsonCosmosSerializer() { }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                Stream stream0 = stream;
                return Unsafe.As<Stream, T>(ref stream0);
            }

            return GetSerializer().Deserialize<T>(stream, encoding: Encoding);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        MemoryStream memoryStream = new ();

        if (input is Stream inputAsStream)
        {
            inputAsStream.CopyTo(memoryStream);
        }
        else
        {
            GetSerializer().Serialize(memoryStream, input, encoding: Encoding);
        }

        memoryStream.Position = 0;

        return memoryStream;
    }

    private static JsonSerializer GetSerializer()
    {
        return JsonSerializer.Create(SerializerSettings);
    }

    private sealed class CosmosContractResolver : IContractResolver
    {
        private readonly IContractResolver decoratee;

        public CosmosContractResolver(IContractResolver decoratee)
        {
            this.decoratee = decoratee;
        }

        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract = decoratee.ResolveContract(type);

            if (type == typeof(CosmosException))
            {
                JsonObjectContract exceptionContract = (JsonObjectContract)contract;
                exceptionContract.Properties.GetClosestMatchProperty(nameof(CosmosException.Diagnostics))!.Ignored = true;
            }

            return contract;
        }
    }
}
