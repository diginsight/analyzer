using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

public interface IPluginService : IDisposable
{
    IEnumerable<Plugin> GetAll();

    Task RegisterDefaultsAsync(CancellationToken cancellationToken);

    Plugin Register(IReadOnlyCollection<Stream> assemblyStreams);

    void Unregister(Guid pluginId);

    IReadOnlyDictionary<string, IAnalyzerStepTemplate> CreateAnalyzerStepTemplates(IServiceProvider serviceProvider);

    IEnumerable<IEventSender> CreateEventSenders(IServiceProvider serviceProvider);
}
