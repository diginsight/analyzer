namespace Diginsight.Analyzer.Repositories;

internal sealed class PhysicalPluginFileRepository : IPluginFileRepository
{
    private readonly string rootPath;

    public PhysicalPluginFileRepository(string rootPath)
    {
        this.rootPath = rootPath;
    }

    public IAsyncEnumerable<IAsyncGrouping<Guid, Stream>> GetSystemPluginsAE()
    {
        return new DirectoryInfo(rootPath)
            .EnumerateDirectories()
            .Select(static (Guid, string)? (dir) => Guid.TryParse(dir.Name, out Guid pluginId) ? (pluginId, dir.FullName) : null)
            .OfType<(Guid PluginId, string DirPath)>()
            .SelectMany(static pair => new DirectoryInfo(pair.DirPath).EnumerateFiles("*.dll").Select(file => (pair.PluginId, Stream: file.OpenRead())))
            .ToAsyncEnumerable()
            .GroupBy(static pair => pair.PluginId, static pair => pair.Stream);
    }
}
