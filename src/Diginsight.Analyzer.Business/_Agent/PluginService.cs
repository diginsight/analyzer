using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;

namespace Diginsight.Analyzer.Business;

internal sealed partial class PluginService : IPluginService
{
    private static readonly AnalysisException DefaultPluginException = new ("Cannot unregister default plugin", HttpStatusCode.Conflict, "DefaultPlugin");

    private readonly ILogger<PluginService> logger;
    private readonly IServiceProvider rootServiceProvider;
    private readonly IAmbientService ambientService;
    private readonly IPluginFileRepository fileRepository;
    private readonly Lock @lock = new ();
    private readonly IDictionary<Guid, PluginHolder> pluginHolders = new Dictionary<Guid, PluginHolder>();

    public PluginService(
        ILogger<PluginService> logger,
        IServiceProvider rootServiceProvider,
        IAmbientService ambientService,
        IPluginFileRepository fileRepository
    )
    {
        this.logger = logger;
        this.rootServiceProvider = rootServiceProvider;
        this.ambientService = ambientService;
        this.fileRepository = fileRepository;
    }

    public IEnumerable<Plugin> GetAll()
    {
        lock (@lock)
        {
            return pluginHolders.Select(static x => new Plugin(x.Key, x.Value.IsDefault, x.Value.AnalyzerStepTemplates));
        }
    }

    public async Task RegisterDefaultsAsync(CancellationToken cancellationToken)
    {
        IEnumerable<IAsyncGrouping<Guid, Stream>> groupings = await fileRepository.GetDefaultPluginsAE().ToArrayAsync(cancellationToken);
        foreach (IAsyncGrouping<Guid, Stream> grouping in groupings)
        {
            IReadOnlyCollection<Stream> assemblyStreams = await grouping.ToArrayAsync(cancellationToken);
            try
            {
                _ = Register(assemblyStreams, grouping.Key, true);
            }
            catch (AnalysisException) { }
            finally
            {
                foreach (Stream assemblyStream in assemblyStreams)
                {
                    await assemblyStream.DisposeAsync();
                }
            }
        }
    }

    public Plugin Register(IReadOnlyCollection<Stream> assemblyStreams)
    {
        return Register(assemblyStreams, ambientService.NewUlid(), false);
    }

    private Plugin Register(IReadOnlyCollection<Stream> assemblyStreams, Guid pluginId, bool isDefault)
    {
        LogMessages.RegisteringPlugin(logger, pluginId, isDefault, assemblyStreams.Count);

        AssemblyLoadContext assemblyLoadContext = new PluginAssemblyLoadContext(pluginId);
        assemblyLoadContext.Unloading += _ => { LogMessages.PluginUnloading(logger, pluginId); };

        foreach (Stream assemblyStream in assemblyStreams)
        {
            try
            {
                _ = assemblyLoadContext.LoadFromStream(assemblyStream);
            }
            catch (Exception exception)
            {
                LogMessages.BadAssembly(logger, pluginId, exception);
                throw new AnalysisException("Bad assembly", HttpStatusCode.BadRequest, "BadAssembly", exception);
            }
        }

        ISet<string> newAnalyzerStepTemplates;
        lock (@lock)
        {
            ISet<string> existingAnalyzerStepTemplates = new HashSet<string>(pluginHolders.Values.SelectMany(static x => x.AnalyzerStepTemplates));
            newAnalyzerStepTemplates = new HashSet<string>();

            foreach (IAnalyzerStepTemplate stepTemplate in CreateInstances<IAnalyzerStepTemplate>(rootServiceProvider, assemblyLoadContext))
            {
                try
                {
                    string stepTemplateName = stepTemplate.Name;
                    if (existingAnalyzerStepTemplates.Contains(stepTemplateName) || !newAnalyzerStepTemplates.Add(stepTemplateName))
                    {
                        LogMessages.DuplicateAnalyzerStepTemplate(logger, pluginId, stepTemplateName);
                        throw new AnalysisException(
                            $"Duplicate analyzer step template '{stepTemplateName}'", HttpStatusCode.Conflict, "DuplicateAnalyzerStepTemplates"
                        );
                    }
                }
                finally
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    (stepTemplate as IDisposable)?.Dispose();
                }
            }

            pluginHolders[pluginId] = new PluginHolder(assemblyLoadContext, isDefault, newAnalyzerStepTemplates);
        }

        LogMessages.PluginRegistered(logger, pluginId);

        return new Plugin(pluginId, isDefault, newAnalyzerStepTemplates);
    }

    private sealed record PluginHolder(AssemblyLoadContext AssemblyLoadContext, bool IsDefault, IEnumerable<string> AnalyzerStepTemplates);

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        public PluginAssemblyLoadContext(Guid pluginId)
            : base($"Plugin {pluginId:D}", true) { }
    }

    public void Unregister(Guid pluginId)
    {
        PluginHolder? pluginHolder;
        lock (@lock)
        {
            if (!pluginHolders.TryGetValue(pluginId, out pluginHolder))
            {
                return;
            }
            if (pluginHolder.IsDefault)
            {
                throw DefaultPluginException;
            }

            pluginHolders.Remove(pluginId);
        }

        try
        {
            pluginHolder.AssemblyLoadContext.Unload();
        }
        catch (Exception e)
        {
            _ = e;
        }

        LogMessages.PluginUnregistered(logger, pluginId);
    }

    public IReadOnlyDictionary<string, IAnalyzerStepTemplate> CreateAnalyzerStepTemplates(IServiceProvider serviceProvider)
    {
        return CreateInstances<IAnalyzerStepTemplate>(serviceProvider)
            .ToDictionary(static x => x.Name);
    }

    public IEnumerable<IEventSender> CreateEventSenders(IServiceProvider serviceProvider)
    {
        return CreateInstances<IEventSender>(serviceProvider).ToArray();
    }

    public void Dispose()
    {
        IReadOnlyCollection<AssemblyLoadContext> assemblyLoadContexts;
        lock (@lock)
        {
            assemblyLoadContexts = pluginHolders.Select(static x => x.Value.AssemblyLoadContext).ToArray();
            pluginHolders.Clear();
        }

        foreach (AssemblyLoadContext assemblyLoadContext in assemblyLoadContexts)
        {
            assemblyLoadContext.Unload();
        }
    }

    private IEnumerable<T> CreateInstances<T>(IServiceProvider serviceProvider)
        where T : class
    {
        IReadOnlyCollection<AssemblyLoadContext> assemblyLoadContexts;
        lock (@lock)
        {
            assemblyLoadContexts = pluginHolders.Select(static x => x.Value.AssemblyLoadContext).ToArray();
        }
        return assemblyLoadContexts.SelectMany(x => CreateInstances<T>(serviceProvider, x));
    }

    private static IEnumerable<T> CreateInstances<T>(IServiceProvider serviceProvider, AssemblyLoadContext alc)
        where T : class
    {
        static Type[] SafeGetExportedTypes(Assembly a)
        {
            try
            {
                return a.GetExportedTypes();
            }
            catch (Exception)
            {
                return [ ];
            }
        }

        T? SafeCreate(Type t)
        {
            try
            {
                return (T)ActivatorUtilities.CreateInstance(serviceProvider, t);
            }
            catch (Exception)
            {
                return null;
            }
        }

        return alc.Assemblies
            .SelectMany(SafeGetExportedTypes)
            .Where(static t => t is { IsClass: true, IsAbstract: false, IsGenericType: false } && typeof(T).IsAssignableFrom(t))
            .Select(SafeCreate)
            .OfType<T>();
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Registring default plugins")]
        internal static partial void RegisteringDefaultPlugins(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Registring plugin {PluginId} (default? {IsDefault}) with {AssemblyCount} assemblies")]
        internal static partial void RegisteringPlugin(ILogger logger, Guid pluginId, bool isDefault, int assemblyCount);

        [LoggerMessage(2, LogLevel.Warning, "Duplicate analyzer step {StepTemplateName} in plugin {PluginId}")]
        internal static partial void DuplicateAnalyzerStepTemplate(ILogger logger, Guid pluginId, string stepTemplateName);

        [LoggerMessage(3, LogLevel.Warning, "Bad assembly while registering plugin {PluginId}")]
        internal static partial void BadAssembly(ILogger logger, Guid? pluginId, Exception exception);

        [LoggerMessage(4, LogLevel.Debug, "Plugin {PluginId} registered successfully")]
        internal static partial void PluginRegistered(ILogger logger, Guid pluginId);

        [LoggerMessage(5, LogLevel.Debug, "Plugin {PluginId} marked for un-registration")]
        internal static partial void PluginUnregistered(ILogger logger, Guid pluginId);

        [LoggerMessage(6, LogLevel.Trace, "Plugin {PluginId} unloading")]
        internal static partial void PluginUnloading(ILogger logger, Guid pluginId);
    }
}
