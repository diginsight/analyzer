using Newtonsoft.Json;

namespace Diginsight.Analyzer.Repositories;

internal static class InternalRepositoriesUtils
{
    private static JsonSerializer? progressSerializer;

    public static JsonSerializer GetProgressSerializer()
    {
        return LazyInitializer.EnsureInitialized(ref progressSerializer, JsonSerializer.CreateDefault);
    }
}
