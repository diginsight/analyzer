using Azure.Core;
using Azure.Identity;
using Diginsight.Analyzer.API.Controllers;
using Diginsight.Analyzer.API.Mvc;
using Diginsight.Analyzer.API.Services;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Repositories;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using AzureCliCredential = Azure.Identity.AzureCliCredential;
using GlobalExceptionFilter = Diginsight.Analyzer.API.Mvc.GlobalExceptionFilter;

namespace Diginsight.Analyzer.API;

internal static partial class Program
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        JsonSerializerSettings settings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
        JsonSerializationGlobals.Adjust(settings);
        JsonConvert.DefaultSettings = () => settings;
    }

    private static async Task Main(string[] args)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        using EarlyLoggingManager earlyLoggingManager = new (
            static source => source.Name.StartsWith("Diginsight.Analyzer.", StringComparison.Ordinal)
        );

        ILogger logger = earlyLoggingManager.LoggerFactory.CreateLogger(typeof(Program));

        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);

        IWebHostEnvironment environment = appBuilder.Environment;
        IConfiguration configuration = appBuilder.Configuration;
        IServiceCollection services = appBuilder.Services;

        services.Configure<DiginsightConsoleFormatterOptions>(configuration.GetSection("Diginsight:Console"));
        services.AddLogging(
            static loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddDiginsightConsole();
            }
        );

        // TODO OpenTelemetry

        earlyLoggingManager.AttachTo(services);

        appBuilder.Host.UseDiginsightServiceProvider();

        TokenCredential credential;
        {
            IConfiguration apiCfgSection = configuration.GetSection("Api");
            Uri appConfigurationEndpoint = new (apiCfgSection["AppConfigurationEndpoint"]!);

            IConfiguration appRegistrationCfgSection = apiCfgSection.GetSection("AppRegistration");
            string tenantId = appRegistrationCfgSection["TenantId"]!;
            string? clientId = appRegistrationCfgSection["ClientId"];
            string? clientSecret = appRegistrationCfgSection["ClientSecret"];

            credential = string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)
                ? new AzureCliCredential(new AzureCliCredentialOptions() { TenantId = tenantId })
                : new ClientSecretCredential(tenantId, clientId, clientSecret);

            appBuilder.Configuration.AddAzureAppConfiguration(
                aaco =>
                {
                    aaco.Connect(appConfigurationEndpoint, credential);
                    aaco.ConfigureKeyVault(aackvo => aackvo.SetCredential(credential));
                }
            );
        }

        bool isLocal = environment.IsDevelopment();
        bool isAgent = CommonUtils.IsAgent;

        LogMessages.ProgramInfos(logger, isAgent);

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(typeof(AnalysisController).FullName!);

        services
            .AddRepositories(configuration, credential)
            .AddBusiness(configuration);

        if (isAgent)
        {
            services
                .AddSingleton<IAgentAmbientService, AgentAmbientService>()
                .AddSingleton<IWaitingService, AgentWaitingService>();

            services.TryAddEnumerable(
                ServiceDescriptor.Transient<IEventSender, AgentWaitingService.EventSender>(
                    static sp => new AgentWaitingService.EventSender((AgentWaitingService)sp.GetRequiredService<IWaitingService>())
                )
            );
        }
        else
        {
            services.AddSingleton<IWaitingService, OrchestratorWaitingService>();
        }

        services.AddHealthChecks();
        // TODO Specific health checks

        services
            .AddMvcCore(
                static options =>
                {
                    options.Filters.Add<GlobalModelStateActionFilter>();
                    options.Filters.Add<GlobalExceptionFilter>();

                    FlavorAwareModelConvention convention = FlavorAwareModelConvention.Instance;
                    options.Conventions.Add((IActionModelConvention)convention);
                    options.Conventions.Add((IParameterModelConvention)convention);

                    options.ModelBinderProviders.Insert(0, QueryBooleanModelBinderProvider.Instance);
                }
            )
            .ConfigureApplicationPartManager(static apm => { apm.FeatureProviders.Add(new FlavorAwareControllerFeatureProvider()); })
            .AddNewtonsoftJson(static options => { JsonSerializationGlobals.Adjust(options.SerializerSettings); });

        services.AddSingleton(static sp => JsonSerializer.Create(sp.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings));

        WebApplication app = appBuilder.Build();
        IServiceProvider serviceProvider = app.Services;

        if (serviceProvider.GetService<IAgentAmbientService>() is { } agentAmbientService)
        {
            LogMessages.AgentPool(logger, agentAmbientService.AgentPool);
        }

        if (!isLocal)
        {
            app.UseHttpsRedirection();
        }

        app.Use(
            static next =>
                httpContext =>
                {
                    httpContext.Request.EnableBuffering();
                    return next(httpContext);
                }
        );

        app.UseRouting();

        app.MapHealthChecks("/healthz");
        app.MapControllers();

        // ReSharper disable once AsyncApostle.AsyncAwaitMayBeElidedHighlighting
        await app.RunAsync();
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Is agent? {IsAgent}")]
        internal static partial void ProgramInfos(ILogger logger, bool isAgent);

        [LoggerMessage(1, LogLevel.Debug, "Agent pool: {AgentPool}")]
        internal static partial void AgentPool(ILogger logger, string agentPool);
    }
}
