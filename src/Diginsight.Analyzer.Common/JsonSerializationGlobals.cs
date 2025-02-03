using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Common;

public static class JsonSerializationGlobals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adjust(JsonSerializerSettings settings)
    {
        if (settings.ContractResolver is CustomContractResolver)
            return;

        Adjust(settings, settings.ContractResolver ?? new DefaultContractResolver());
    }

    private static void Adjust(JsonSerializerSettings settings, IContractResolver contractResolver)
    {
        if (contractResolver is DefaultContractResolver dcr)
        {
            dcr.NamingStrategy = new CamelCaseNamingStrategy();
        }

        settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        settings.ContractResolver = new CustomContractResolver(contractResolver);
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    }

    private sealed class CustomContractResolver : IContractResolver
    {
        private readonly IContractResolver decoratee;
        private readonly ISet<Type> seenTypes = new HashSet<Type>();

        public CustomContractResolver(IContractResolver decoratee)
        {
            this.decoratee = decoratee;
        }

        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract = decoratee.ResolveContract(type);
            if (!seenTypes.Add(type))
            {
                return contract;
            }

            if (typeof(Exception).IsAssignableFrom(type))
            {
                JsonContainerContract exceptionContract = (JsonContainerContract)contract;
                exceptionContract.ItemTypeNameHandling = TypeNameHandling.Auto;
            }

            return contract;
        }
    }
}
