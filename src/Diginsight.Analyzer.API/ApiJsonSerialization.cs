using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.API;

public static class ApiJsonSerialization
{
    private static readonly ConcurrentDictionary<JsonSerializerSettings, ValueTuple> seenSettings = new ();

    [ModuleInitializer]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void InitializeModule()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(EntitiesJsonSerialization).Module.ModuleHandle);

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

        EntitiesJsonSerialization.Adjust(settings);
    }
}
