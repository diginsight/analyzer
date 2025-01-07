using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Repositories;

internal sealed class BlobAnalysisFileRepository : IAnalysisFileRepository
{
    private const string QueuedDN = "_queued";
    private const string AttemptsDN = "attempts";
    private const string InputPayloadsDN = "inPayloads";
    private const string OutputPayloadsDN = "outPayloads";
    private const string TemporaryPayloadsDN = "tmpPayloads";
    private const string DefinitionFN = "definition.yaml";
    private const string ProgressFN = "progress.json";

    private readonly BlobContainerClient containerClient;

    public BlobAnalysisFileRepository(IOptions<RepositoriesOptions> repositoriesOptions)
    {
        IRepositoriesOptions repositoriesOptions0 = repositoriesOptions.Value;
        containerClient = repositoriesOptions0.BlobServiceClient.GetBlobContainerClient("analysis");
    }

    private static BlobHttpHeaders ToBlobHttpHeaders(EncodedStream encodedStream, string mediaType)
    {
        return new BlobHttpHeaders()
        {
            ContentType = new MediaTypeHeaderValue(mediaType, encodedStream.Encoding.WebName).ToString(),
        };
    }

    private static BlobHttpHeaders ToBlobHttpHeaders(NamedEncodedStream encodedStream)
    {
        BlobHttpHeaders blobHttpHeaders = ToBlobHttpHeaders(encodedStream, encodedStream.ContentType);
        blobHttpHeaders.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = encodedStream.Name }.ToString();
        return blobHttpHeaders;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractProperties(string rawContentType, out Encoding encoding, out MediaTypeHeaderValue mthv)
    {
        mthv = MediaTypeHeaderValue.Parse(rawContentType);
        encoding = Encoding.GetEncoding(mthv.CharSet!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractProperties(
        string rawContentType, string rawContentDisposition, out Encoding encoding, out string fileName, out string contentType
    )
    {
        ExtractProperties(rawContentType, out encoding, out MediaTypeHeaderValue mthv);
        ContentDispositionHeaderValue cdhv = ContentDispositionHeaderValue.Parse(rawContentDisposition);
        fileName = (cdhv.FileNameStar ?? cdhv.FileName)!;
        contentType = mthv.MediaType!;
    }

    private static async Task<EncodedStream> ToEncodedStreamAsync(BlobBaseClient blobClient, CancellationToken cancellationToken)
    {
        BlobProperties blobProperties = (await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        ExtractProperties(blobProperties.ContentType, out Encoding encoding, out _);
        return new EncodedStream(await blobClient.OpenReadAsync(cancellationToken: cancellationToken), encoding);
    }

    private static async Task<NamedEncodedStream> ToNamedEncodedStreamAsync(BlobBaseClient blobClient, CancellationToken cancellationToken)
    {
        BlobProperties blobProperties = (await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        ExtractProperties(
            blobProperties.ContentType, blobProperties.ContentDisposition, out Encoding encoding, out string fileName, out string contentType
        );
        return new NamedEncodedStream(await blobClient.OpenReadAsync(cancellationToken: cancellationToken), encoding, fileName, contentType);
    }

    public Task WriteDefinitionAsync(EncodedStream encodedStream, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;
        return WriteDefinitionAsync(encodedStream, $"{analysisId:D}{(attempt > 0 ? $"/{AttemptsDN}/{attempt.ToStringInvariant()}" : "")}", cancellationToken);
    }

    public Task WriteQueuedDefinitionAsync(EncodedStream encodedStream, Guid executionId, CancellationToken cancellationToken)
    {
        return WriteDefinitionAsync(encodedStream, $"{QueuedDN}/{executionId:D}", cancellationToken);
    }

    private Task WriteDefinitionAsync(EncodedStream encodedStream, string path, CancellationToken cancellationToken)
    {
        return containerClient
            .GetBlobClient($"{path}/{DefinitionFN}")
            .UploadAsync(
                encodedStream.Stream,
                new BlobUploadOptions() { HttpHeaders = ToBlobHttpHeaders(encodedStream, IAnalysisFileRepository.DefinitionContentType) },
                cancellationToken: cancellationToken
            );
    }

    public async Task<EncodedStream> ReadDefinitionAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        BlobClient finalBlobClient;
        if (attempt > 0)
        {
            BlobClient specificBlobClient = containerClient.GetBlobClient($"{analysisId:D}/{AttemptsDN}/{attempt.ToStringInvariant()}/{DefinitionFN}");
            if (!await specificBlobClient.ExistsAsync(cancellationToken))
            {
                return new EncodedStream(Stream.Null, CommonUtils.DefaultEncoding);
            }

            finalBlobClient = specificBlobClient;
        }
        else
        {
            finalBlobClient = containerClient.GetBlobClient($"{analysisId:D}/{DefinitionFN}");
        }

        return await ToEncodedStreamAsync(finalBlobClient, cancellationToken);
    }

    public async Task WriteProgressAsync(JObject progress, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        await using Stream stream = await containerClient
            .GetBlobClient($"{analysisId:D}/{AttemptsDN}/{attempt.ToStringInvariant()}/{ProgressFN}")
            .OpenWriteAsync(true, cancellationToken: cancellationToken);
        await InternalRepositoriesUtils.GetProgressSerializer().SerializeAsync(stream, progress, encoding: CommonUtils.DefaultEncoding);
    }

    public async Task<JObject?> ReadProgressAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;
        BlobClient blobClient = containerClient.GetBlobClient($"{analysisId:D}/{AttemptsDN}/{attempt.ToStringInvariant()}/{ProgressFN}");

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        await using Stream stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return await InternalRepositoriesUtils.GetProgressSerializer().DeserializeAsync<JObject>(stream, encoding: CommonUtils.DefaultEncoding);
    }

    public Task WriteInputPayloadAsync(NamedEncodedStream encodedStream, Guid analysisId, string label, CancellationToken cancellationToken)
    {
        return WriteInputPayloadAsync(encodedStream, analysisId.ToString("D"), label, cancellationToken);
    }

    public Task WriteQueuedInputPayloadAsync(NamedEncodedStream encodedStream, Guid executionId, string label, CancellationToken cancellationToken)
    {
        return WriteInputPayloadAsync(encodedStream, $"{QueuedDN}/{executionId:D}", label, cancellationToken);
    }

    private Task WriteInputPayloadAsync(NamedEncodedStream encodedStream, string path, string label, CancellationToken cancellationToken)
    {
        return containerClient
            .GetBlobClient($"{path}/{InputPayloadsDN}/{Uri.EscapeDataString(label)}")
            .UploadAsync(encodedStream.Stream, new BlobUploadOptions() { HttpHeaders = ToBlobHttpHeaders(encodedStream) }, cancellationToken);
    }

    public Task WriteOutputPayloadAsync(NamedEncodedStream encodedStream, AnalysisCoord coord, string label, bool temporary, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;
        return containerClient
            .GetBlobClient($"{analysisId:D}/{AttemptsDN}/{attempt.ToStringInvariant()}/{(temporary ? TemporaryPayloadsDN : OutputPayloadsDN)}/{Uri.EscapeDataString(label)}")
            .UploadAsync(encodedStream.Stream, new BlobUploadOptions() { HttpHeaders = ToBlobHttpHeaders(encodedStream) }, cancellationToken);
    }

    public async Task<NamedEncodedStream?> ReadPayloadAsync(AnalysisCoord coord, string label, CancellationToken cancellationToken)
    {
        IEnumerable<string> GetPossibleBlobNames()
        {
            (Guid analysisId, int attempt) = coord;

            string analysisId0 = analysisId.ToString("D");
            yield return $"{analysisId0}/{InputPayloadsDN}/{Uri.EscapeDataString(label)}";

            string attempt0 = attempt.ToStringInvariant();
            yield return $"{analysisId0}/{AttemptsDN}/{attempt0}/{OutputPayloadsDN}/{Uri.EscapeDataString(label)}";
            yield return $"{analysisId0}/{AttemptsDN}/{attempt0}/{TemporaryPayloadsDN}/{Uri.EscapeDataString(label)}";
        }

        BlobClient? blobClient = await GetPossibleBlobNames()
            .ToAsyncEnumerable()
            .Select(x => containerClient.GetBlobClient(x))
            .FirstOrDefaultAwaitWithCancellationAsync(static async (x, ct) => await x.ExistsAsync(ct), cancellationToken);

        return blobClient is null ? null : await ToNamedEncodedStreamAsync(blobClient, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetBlobNameLeaf(string blobName)
    {
        return blobName[(blobName.LastIndexOf('/') + 1)..];
    }

    public async IAsyncEnumerable<PayloadDescriptor> GetPayloadDescriptorsAE(AnalysisCoord coord, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        static PayloadDescriptor ToPayloadDescriptor(BlobItem blobItem, bool isOutput)
        {
            BlobItemProperties blobProperties = blobItem.Properties;
            ExtractProperties(
                blobProperties.ContentType, blobProperties.ContentDisposition, out Encoding encoding, out string fileName, out string contentType
            );
            return new PayloadDescriptor(GetBlobNameLeaf(blobItem.Name), isOutput, encoding, fileName, contentType);
        }

        await foreach (BlobItem blobItem in
            containerClient.GetBlobsAsync(prefix: $"{analysisId:D}/{InputPayloadsDN}/", cancellationToken: cancellationToken))
        {
            yield return ToPayloadDescriptor(blobItem, false);
        }

        await foreach (BlobItem blobItem in
            containerClient.GetBlobsAsync(prefix: $"{analysisId:D}/{AttemptsDN}/{attempt.ToStringInvariant()}/{OutputPayloadsDN}/", cancellationToken: cancellationToken))
        {
            yield return ToPayloadDescriptor(blobItem, true);
        }
    }

    public async Task MoveQueuedTreeAsync(Guid executionId, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        ICollection<Task> tasks = new List<Task>();

        async Task MoveBlobAsync(BlobBaseClient sourceBlobClient, BlobBaseClient targetBlobClient)
        {
            CopyFromUriOperation operation = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: cancellationToken);
            await operation.WaitForCompletionAsync(cancellationToken: cancellationToken);
            await sourceBlobClient.DeleteAsync(cancellationToken: cancellationToken);
        }

        {
            BlobClient sourceBlobClient = containerClient.GetBlobClient($"{QueuedDN}/{executionId:D}/{DefinitionFN}");
            if (await sourceBlobClient.ExistsAsync(cancellationToken))
            {
                BlobClient targetBlobClient = containerClient.GetBlobClient($"{analysisId:D}/{(attempt > 0 ? $"{AttemptsDN}/{attempt.ToStringInvariant()}" : "")}/{DefinitionFN}");
                tasks.Add(MoveBlobAsync(sourceBlobClient, targetBlobClient));
            }
        }

        await foreach (BlobItem blobItem in
            containerClient.GetBlobsAsync(prefix: $"{QueuedDN}/{executionId:D}/{InputPayloadsDN}/"))
        {
            string blobName = blobItem.Name;
            BlobClient sourceBlobClient = containerClient.GetBlobClient(blobName);
            BlobClient targetBlobClient = containerClient.GetBlobClient($"{analysisId:D}/{InputPayloadsDN}/{GetBlobNameLeaf(blobName)}");
            tasks.Add(MoveBlobAsync(sourceBlobClient, targetBlobClient));
        }

        await Task.WhenAll(tasks);
    }

    public async Task DeleteQueuedTreeAsync(Guid executionId)
    {
        ICollection<Task> tasks = new List<Task>();

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: $"{QueuedDN}/{executionId:D}/"))
        {
            tasks.Add(containerClient.GetBlobClient(blobItem.Name).DeleteAsync());
        }

        // ReSharper disable once AsyncApostle.AsyncAwaitMayBeElidedHighlighting
        await Task.WhenAll(tasks);
    }

    public async Task DeleteTreeAsync(AnalysisCoord coord)
    {
        (Guid analysisId, int attempt) = coord;

        ICollection<Task> attemptTasks = new List<Task>();

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: $"{analysisId:D}/attempt/{attempt.ToStringInvariant()}/"))
        {
            attemptTasks.Add(containerClient.GetBlobClient(blobItem.Name).DeleteAsync());
        }

        await Task.WhenAll(attemptTasks);

        if (!await containerClient.GetBlobsAsync(prefix: $"{analysisId:D}/attempt/").AnyAsync())
        {
            return;
        }

        ICollection<Task> rootTasks = new List<Task>();

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: $"{analysisId:D}/"))
        {
            rootTasks.Add(containerClient.GetBlobClient(blobItem.Name).DeleteAsync());
        }

        await Task.WhenAll(rootTasks);
    }
}
