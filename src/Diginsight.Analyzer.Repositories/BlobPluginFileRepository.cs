using Azure.Storage.Blobs;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Repositories;

internal sealed class BlobPluginFileRepository : IPluginFileRepository
{
    private readonly BlobContainerClient containerClient;

    public BlobPluginFileRepository(IOptions<RepositoriesOptions> repositoriesOptions)
    {
        IRepositoriesOptions repositoriesOptions0 = repositoriesOptions.Value;
        containerClient = repositoriesOptions0.BlobServiceClient.GetBlobContainerClient("plugins");
    }

    public IAsyncEnumerable<IAsyncGrouping<Guid, Stream>> GetDefaultPluginsAE()
    {
        return containerClient.GetBlobsAsync()
            .Select(static item => item.Name.Split('/'))
            .Where(static segments => segments.Length == 2)
            .Select(
                static (Guid PluginId, string FullName)? (segments) =>
                {
                    string fullName = $"{segments[0]}/{segments[1]}";
                    return Guid.TryParse(segments[0], out Guid pluginId) && segments[1].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        ? (pluginId, fullName)
                        : null;
                }
            )
            .Where(static pair => pair is not null)
            .Select(static pair => pair!.Value)
            .GroupByAwaitWithCancellation(
                static (pair, _) => ValueTask.FromResult(pair.PluginId),
                async (pair, ct) => await containerClient.GetBlobClient(pair.FullName).OpenReadAsync(cancellationToken: ct)
            );
    }
}
