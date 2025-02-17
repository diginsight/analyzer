using Azure.Core;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;

namespace Diginsight.Analyzer.Repositories;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class RepositoriesExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration, TokenCredential credential)
    {
        if (services.Any(static x => x.ServiceType == typeof(IAnalysisInfoRepository)))
        {
            return services;
        }

        services.Configure<RepositoriesOptions>(configuration.GetSection("Repositories"));
        services.PostConfigure<RepositoriesOptions>(options => { options.Credential = credential; });

        static TService CreateFileRepository<TService, TBlobImpl, TFileImpl>(IServiceProvider sp, string path)
            where TBlobImpl : class, TService
            where TFileImpl : class, TService
        {
            IRepositoriesOptions repositoriesOptions = sp.GetRequiredService<IOptions<RepositoriesOptions>>().Value;
            switch (repositoriesOptions.FileImplementation)
            {
                case FileImplementation.Blob:
                    return ActivatorUtilities.CreateInstance<TBlobImpl>(sp);

                case FileImplementation.Physical:
                {
                    IHostEnvironment hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
                    string? physicalFileRoot = repositoriesOptions.PhysicalFileRoot.HardTrim();
                    string rootPath = Path.Combine(hostEnvironment.ContentRootPath, Environment.ExpandEnvironmentVariables(physicalFileRoot ?? ""), path);
                    return ActivatorUtilities.CreateInstance<TFileImpl>(sp, rootPath);
                }

                default:
                    throw new UnreachableException($"Unrecognized {nameof(FileImplementation)}");
            }
        }

        services
            .AddSingleton<IAnalysisInfoRepository, AnalysisInfoRepository>()
            .AddSingleton<IPermissionAssignmentRepository, PermissionAssignmentRepository>()
            .AddSingleton<ILeaseRepository, LeaseRepository>()
            .AddSingleton(
                static sp => CreateFileRepository<IAnalysisFileRepository, BlobAnalysisFileRepository, PhysicalAnalysisFileRepository>(sp, "analyses")
            )
            .AddSingleton(
                static sp => CreateFileRepository<IPluginFileRepository, BlobPluginFileRepository, PhysicalPluginFileRepository>(sp, "plugins")
            );

        return services;
    }
}
