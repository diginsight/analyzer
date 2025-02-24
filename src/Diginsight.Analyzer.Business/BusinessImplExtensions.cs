using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StringTokenFormatter;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class BusinessImplExtensions
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(static x => x.ServiceType == typeof(IAmbientService)))
        {
            return services;
        }

        services
            .AddSingleton<IAmbientService, AmbientService>()
            .AddSingleton<IConfigureOptions<CoreOptions>, ConfigureCoreOptions>()
            .Configure<CoreOptions>(configuration.GetSection("Core"))
            .AddSingleton<ISnapshotService, SnapshotService>()
#if FEATURE_REPORTS
            .AddSingleton<IReportService, ReportService>()
#endif
            .AddSingleton<IInternalAnalysisService, InternalAnalysisService>();

        if (CommonUtils.IsAgent)
        {
            services
                .AddSingleton<IAgentExecutionService, AgentExecutionService>()
                .AddSingleton<IAgentLeaseService, AgentLeaseService>()
                .AddSingleton<IAgentAnalysisService, AgentAnalysisService>()
                .AddSingleton<IAnalysisService>(static p => p.GetRequiredService<IAgentAnalysisService>())
                .AddSingleton<IAgentPayloadService, PayloadService>()
                .AddSingleton<IPayloadService>(static p => p.GetRequiredService<IAgentPayloadService>())
                .AddSingleton<IAgentAnalysisContextFactory, AgentAnalysisContextFactory>()
                .AddScoped<IAnalysisExecutor, AnalysisExecutor>()
                .AddSingleton<IEventService, EventService>()
                .AddSingleton<IPluginService, PluginService>()
                .AddSingleton<ICompilerFactory, CompilerFactory>()
                .AddSingleton<IOnCreateServiceProvider, RegisterAgentLifetimeActions>();
        }
        else
        {
            services
                .AddSingleton<IOrchestratorExecutionService, OrchestratorExecutionService>()
                .AddSingleton<IOrchestratorLeaseService, OrchestratorLeaseService>()
                .AddSingleton<IOrchestratorAnalysisService, OrchestratorAnalysisService>()
                .AddSingleton<IAnalysisService>(static p => p.GetRequiredService<IOrchestratorAnalysisService>())
                .AddSingleton<IPayloadService, PayloadService>()
                .AddSingleton<IOrchestratorAnalysisContextFactory, OrchestratorAnalysisContextFactory>()
                .AddSingleton<IAgentClientFactory, AgentClientFactory>()
                .AddSingleton<IDequeuerService, DequeuerService>()
                .AddHostedService(static p => p.GetRequiredService<IDequeuerService>())
                .AddHttpClient(typeof(AgentClient).FullName!)
                .ConfigureHttpClient(
                    static (sp, hc) =>
                    {
                        IOrchestratorCoreOptions coreOptions = sp.GetRequiredService<IOptions<CoreOptions>>().Value;
                        hc.Timeout = TimeSpan.FromSeconds(coreOptions.AgentTimeoutSeconds);
                    }
                );
        }

        return services;
    }

    private sealed class RegisterAgentLifetimeActions : IOnCreateServiceProvider
    {
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly IAgentExecutionService agentExecutionService;
        private readonly IAgentLeaseService agentLeaseService;
        private readonly IPluginService pluginService;

        public RegisterAgentLifetimeActions(
            IHostApplicationLifetime applicationLifetime,
            IAgentExecutionService agentExecutionService,
            IAgentLeaseService agentLeaseService,
            IPluginService pluginService
        )
        {
            this.applicationLifetime = applicationLifetime;
            this.agentExecutionService = agentExecutionService;
            this.agentLeaseService = agentLeaseService;
            this.pluginService = pluginService;
        }

        public void Run()
        {
            applicationLifetime.ApplicationStopping.Register(
                () => { agentExecutionService.WaitForFinishAsync().GetAwaiter().GetResult(); }
            );

            applicationLifetime.ApplicationStarted.Register(
                () => { agentLeaseService.CreateAsync().GetAwaiter().GetResult(); }
            );
            applicationLifetime.ApplicationStopping.Register(
                () => { agentLeaseService.DeleteAsync().GetAwaiter().GetResult(); }
            );

            applicationLifetime.ApplicationStarted.Register(
                () => { pluginService.RegisterSystemAsync(applicationLifetime.ApplicationStopping).GetAwaiter().GetResult(); }
            );
        }
    }

    private sealed class ConfigureCoreOptions : IConfigureOptions<CoreOptions>
    {
        private readonly IHostEnvironment hostEnvironment;

        public ConfigureCoreOptions(IHostEnvironment hostEnvironment)
        {
            this.hostEnvironment = hostEnvironment;
        }

        public void Configure(CoreOptions coreOptions)
        {
            if (hostEnvironment.IsDevelopment())
            {
                coreOptions.DefaultParallelism = 1;
                coreOptions.DefaultAgentPool = $"local_{Environment.MachineName}";
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TokenValueContainerBuilder AddReason(this TokenValueContainerBuilder containerBuilder, IFailableRO failable)
    {
        return failable.Reason is { } reason
            ? containerBuilder.AddPrefixedObject("reason", new PlaceholderReplacer.ExceptionView(reason))
            : containerBuilder;
    }
}
