using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Graph;

namespace Diginsight.Analyzer.Repositories.Configurations;

internal interface IRepositoriesOptions : IDisposable
{
    FileImplementation FileImplementation { get; }

    string? PhysicalFileRoot { get; }

    int TimedProgressFlushSeconds { get; }

    TokenCredential Credential { get; }

    CosmosClient CosmosClient { get; }

    BlobServiceClient BlobServiceClient { get; }

    GraphServiceClient GraphServiceClient { get; }
}
