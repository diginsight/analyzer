using Diginsight.Analyzer.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Entities;

public static class EntitiesJsonSerialization
{
    private static readonly ConcurrentDictionary<JsonSerializerSettings, ValueTuple> seenSettings = new();

    [ModuleInitializer]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void InitializeModule()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(CommonJsonSerialization).Module.ModuleHandle);

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

        CommonJsonSerialization.Adjust(settings);

        settings.ContractResolver = new CustomContractResolver(settings.ContractResolver ?? new DefaultContractResolver());
    }

    private sealed class CustomContractResolver : IContractResolver
    {
        private readonly Lazy<NamingStrategy?> namingStrategyLazy;
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

            namingStrategyLazy = new Lazy<NamingStrategy?>(
                () =>
                {
                    IContractResolver cr = Decoratee;
                    while (cr is IContractResolverDecorator crd)
                    {
                        cr = crd.Decoratee;
                    }

                    return (cr as DefaultContractResolver)?.NamingStrategy;
                }
            );
        }

        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract = Decoratee.ResolveContract(type);
            if (!seenTypes.TryAdd(type, default))
            {
                return contract;
            }

            if (typeof(IPermissionAssignment).IsAssignableFrom(type) && !type.IsAbstract)
            {
                contract.Converter = null;
            }

            return contract;
        }
    }
}
