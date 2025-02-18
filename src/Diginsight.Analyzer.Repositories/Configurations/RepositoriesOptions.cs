using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Graph;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Repositories.Configurations;

internal sealed class RepositoriesOptions : IRepositoriesOptions
{
    private FileImplementation? fileImplementation;
    private CosmosClient? cosmosClient;
    private GraphServiceClient? graphServiceClient;

    public string? FileImplementation { get; set; }

    public string? PhysicalFileRoot { get; set; }

    public int TimedProgressFlushSeconds { get; set; } = 30;

    public string? CosmosAccountEndpoint { get; set; }

    public string? BlobStorageUri { get; set; }

    [DisallowNull]
    internal TokenCredential? Credential { private get; set; }

    FileImplementation IRepositoriesOptions.FileImplementation =>
        fileImplementation ??= FileImplementation.HardTrim() is { } str
            ? Enum.Parse<FileImplementation>(str, true)
            : Configurations.FileImplementation.Blob;

    TokenCredential IRepositoriesOptions.Credential => Credential ?? throw new InvalidOperationException($"{nameof(Credential)} is unset");

    CosmosClient IRepositoriesOptions.CosmosClient => cosmosClient ??= new CosmosClient(
        CosmosAccountEndpoint ?? throw new InvalidOperationException($"{nameof(CosmosAccountEndpoint)} is unset"),
        Credential,
        new CosmosClientOptions()
        {
            Serializer = NewtonsoftJsonCosmosSerializer.Instance,
            CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions() { DisableDistributedTracing = false },
        }
    );

    [field: MaybeNull]
    BlobServiceClient IRepositoriesOptions.BlobServiceClient => field ??= new BlobServiceClient(
        new Uri(BlobStorageUri ?? throw new InvalidOperationException($"{nameof(BlobStorageUri)} is unset")),
        Credential
    );

    GraphServiceClient IRepositoriesOptions.GraphServiceClient => graphServiceClient ??= new GraphServiceClient(Credential);

    void IDisposable.Dispose()
    {
        Interlocked.Exchange(ref cosmosClient, null)?.Dispose();
        Interlocked.Exchange(ref graphServiceClient, null)?.Dispose();
    }
}
