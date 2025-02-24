namespace Diginsight.Analyzer.Repositories;

public interface IPluginFileRepository
{
    IAsyncEnumerable<IAsyncGrouping<Guid, Stream>> GetSystemPluginsAE();
}
