using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Common;

public static class CommonJsonSerialization
{
    private static readonly ConcurrentDictionary<JsonSerializerSettings, ValueTuple> seenSettings = new();

    [ModuleInitializer]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void InitializeModule()
    {
        Func<JsonSerializerSettings>? makeSettings = JsonConvert.DefaultSettings;
        JsonConvert.DefaultSettings = () =>
        {
            JsonSerializerSettings settings = makeSettings?.Invoke() ?? new JsonSerializerSettings();
            Adjust(settings);
            return settings;
        };
    }

    public static void Adjust(JsonSerializerSettings settings)
    {
        if (!seenSettings.TryAdd(settings, default))
            return;

        IContractResolver contractResolver = settings.ContractResolver ?? new DefaultContractResolver();

        if (contractResolver is DefaultContractResolver dcr)
        {
            dcr.NamingStrategy = new CamelCaseNamingStrategy();
        }

        settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        settings.ContractResolver = new CustomContractResolver(contractResolver);
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    }

    private sealed class CustomContractResolver : IContractResolverDecorator
    {
        private readonly ConcurrentDictionary<Type, ValueTuple> seenTypes = new ();
        private bool wasRead;

        public IContractResolver Decoratee
        {
            get
            {
                wasRead = true;
                return field;
            }
            set
            {
                if (wasRead)
                    throw new InvalidOperationException($"{nameof(Decoratee)} has already been read");
                field = value;
            }
        }

        public CustomContractResolver(IContractResolver decoratee)
        {
            Decoratee = decoratee;
        }

        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract = Decoratee.ResolveContract(type);
            if (!seenTypes.TryAdd(type, default))
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
