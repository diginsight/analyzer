using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Repositories;

internal sealed class PhysicalAnalysisFileRepository : IAnalysisFileRepository
{
    private const string QueuedDN = "_queued";
    private const string AttemptsDN = "attempts";
    private const string InputPayloadsDN = "inPayloads";
    private const string OutputPayloadsDN = "outPayloads";
    private const string TemporaryPayloadsDN = "tmpPayloads";
    private const string DefinitionFN = "definition.yaml";
    private const string ProgressFN = "progress.json";

    private static readonly CompositeFormat MetaFNFormat = CompositeFormat.Parse(".{0}.meta");

    private readonly JsonSerializer jsonSerializer;
    private readonly string rootPath;

    public PhysicalAnalysisFileRepository(JsonSerializer jsonSerializer, string rootPath)
    {
        this.jsonSerializer = jsonSerializer;
        this.rootPath = rootPath;
    }

    private async Task CoreWriteAsync(EncodedStream encodedStream, DirectoryInfo directory, string fn, CancellationToken cancellationToken)
    {
        string directoryPath = directory.FullName;

        await using (Stream stream = File.OpenWrite(Path.Combine(directoryPath, fn)))
        {
            await encodedStream.Stream.CopyToAsync(stream, cancellationToken);
        }
        await using (Stream stream = File.OpenWrite(Path.Combine(directoryPath, string.Format(CultureInfo.InvariantCulture, MetaFNFormat, fn))))
        {
            await jsonSerializer.SerializeAsync(stream, new Metadata(encodedStream), encoding: CommonUtils.DefaultEncoding);
        }
    }

    public Task WriteDefinitionAsync(EncodedStream encodedStream, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(rootPath, analysisId.ToString("D")));
        if (attempt > 0)
        {
            directory = new DirectoryInfo(Path.Combine(directory.FullName, AttemptsDN, attempt.ToStringInvariant()));
        }
        return WriteDefinitionAsync(encodedStream, directory, cancellationToken);
    }

    public Task WriteQueuedDefinitionAsync(EncodedStream encodedStream, Guid executionId, CancellationToken cancellationToken)
    {
        DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(rootPath, QueuedDN, executionId.ToStringInvariant()));
        directory.Create();
        return WriteDefinitionAsync(encodedStream, directory, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task WriteDefinitionAsync(EncodedStream encodedStream, DirectoryInfo directory, CancellationToken cancellationToken)
    {
        return CoreWriteAsync(encodedStream, directory, DefinitionFN, cancellationToken);
    }

    public async Task<EncodedStream> ReadDefinitionAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        FileInfo finalFile;
        if (attempt > 0)
        {
            FileInfo specificFile = new (Path.Combine(rootPath, analysisId.ToString("D"), AttemptsDN, attempt.ToStringInvariant(), DefinitionFN));
            if (!specificFile.Exists)
            {
                return new EncodedStream(Stream.Null, CommonUtils.DefaultEncoding);
            }

            finalFile = specificFile;
        }
        else
        {
            finalFile = new FileInfo(Path.Combine(rootPath, analysisId.ToString("D"), DefinitionFN));
        }

        return await ToEncodedStreamAsync(finalFile);
    }

    public async Task WriteProgressAsync(JObject progress, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(rootPath, analysisId.ToString("D"), AttemptsDN, attempt.ToStringInvariant()));

        await using Stream stream = File.OpenWrite(Path.Combine(directory.FullName, ProgressFN));
        await InternalRepositoriesUtils.GetProgressSerializer().SerializeAsync(stream, progress, encoding: CommonUtils.DefaultEncoding);
    }

    public async Task<JObject?> ReadProgressAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        FileInfo file = new (Path.Combine(rootPath, analysisId.ToString("D"), AttemptsDN, attempt.ToStringInvariant(), ProgressFN));
        if (!file.Exists)
        {
            return null;
        }

        await using Stream stream = file.OpenRead();
        return await InternalRepositoriesUtils.GetProgressSerializer().DeserializeAsync<JObject>(stream, encoding: CommonUtils.DefaultEncoding);
    }

    public Task WriteInputPayloadAsync(NamedEncodedStream encodedStream, Guid analysisId, string label, CancellationToken cancellationToken)
    {
        DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(rootPath, analysisId.ToString("D")));
        return WriteInputPayloadAsync(encodedStream, directory, label, cancellationToken);
    }

    public Task WriteQueuedInputPayloadAsync(NamedEncodedStream encodedStream, Guid executionId, string label, CancellationToken cancellationToken)
    {
        DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(rootPath, QueuedDN, executionId.ToString("D")));
        return WriteInputPayloadAsync(encodedStream, directory, label, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task WriteInputPayloadAsync(NamedEncodedStream encodedStream, DirectoryInfo directory, string label, CancellationToken cancellationToken)
    {
        return CoreWriteAsync(encodedStream, Directory.CreateDirectory(Path.Combine(directory.FullName, InputPayloadsDN)), label, cancellationToken);
    }

    public Task WriteOutputPayloadAsync(NamedEncodedStream encodedStream, AnalysisCoord coord, string label, bool temporary, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        DirectoryInfo directory = Directory.CreateDirectory(
            Path.Combine(rootPath, analysisId.ToString("D"), AttemptsDN, attempt.ToStringInvariant(), temporary ? TemporaryPayloadsDN : OutputPayloadsDN)
        );
        return CoreWriteAsync(encodedStream, directory, label, cancellationToken);
    }

    public async Task<NamedEncodedStream?> ReadPayloadAsync(AnalysisCoord coord, string label, CancellationToken cancellationToken)
    {
        IEnumerable<string> GetPossiblePaths()
        {
            (Guid analysisId, int attempt) = coord;

            string analysisId0 = analysisId.ToString("D");
            yield return Path.Combine(analysisId0, InputPayloadsDN, label);

            string attempt0 = attempt.ToStringInvariant();
            yield return Path.Combine(analysisId0, AttemptsDN, attempt0, OutputPayloadsDN, label);
            yield return Path.Combine(analysisId0, AttemptsDN, attempt0, TemporaryPayloadsDN, label);
        }

        FileInfo? file = GetPossiblePaths()
            .Select(x => new FileInfo(Path.Combine(rootPath, x)))
            .FirstOrDefault(static x => x.Exists);

        return file is null ? null : await ToNamedEncodedStreamAsync(file);
    }

    private async Task<Metadata> ReadMetadataAsync(FileInfo file)
    {
        await using Stream stream = File.OpenRead(Path.Combine(file.FullName, "..", string.Format(CultureInfo.InvariantCulture, MetaFNFormat, file.Name)));
        return await jsonSerializer.DeserializeAsync<Metadata>(stream, encoding: CommonUtils.DefaultEncoding);
    }

    private async Task<EncodedStream> ToEncodedStreamAsync(FileInfo file)
    {
        Metadata metadata = await ReadMetadataAsync(file);
        return new EncodedStream(file.OpenRead(), metadata.Encoding);
    }

    private async Task<NamedEncodedStream> ToNamedEncodedStreamAsync(FileInfo file)
    {
        Metadata metadata = await ReadMetadataAsync(file);
        return new NamedEncodedStream(file.OpenRead(), metadata.Encoding, metadata.Name!, metadata.ContentType!);
    }

    private async Task<PayloadDescriptor> ToPayloadDescriptorAsync(FileInfo file)
    {
        Metadata metadata = await ReadMetadataAsync(file);
        return new PayloadDescriptor(file.Name, file.Directory!.Name == OutputPayloadsDN, metadata.Encoding, metadata.Name!, metadata.ContentType!);
    }

    public IAsyncEnumerable<PayloadDescriptor> GetPayloadDescriptorsAE(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;
        string analysisPath = Path.Combine(rootPath, analysisId.ToString("D"));

        DirectoryInfo inputDirectory = new (Path.Combine(analysisPath, InputPayloadsDN));
        DirectoryInfo outputDirectory = new (Path.Combine(analysisPath, AttemptsDN, attempt.ToStringInvariant(), OutputPayloadsDN));

        return CoreGetAE(inputDirectory, cancellationToken).Concat(CoreGetAE(outputDirectory, cancellationToken));

        async IAsyncEnumerable<PayloadDescriptor> CoreGetAE(DirectoryInfo directory, [EnumeratorCancellation] CancellationToken ct)
        {
            if (directory.Exists)
            {
                foreach (FileInfo file in directory.EnumerateFiles().Where(static x => !x.Name.StartsWith('.')))
                {
                    yield return await ToPayloadDescriptorAsync(file);
                }
            }
        }
    }

    public Task MoveQueuedTreeAsync(Guid executionId, AnalysisCoord coord, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = coord;

        string executionPath = Path.Combine(rootPath, QueuedDN, executionId.ToString("D"));
        string analysisPath = Path.Combine(rootPath, analysisId.ToString("D"));
        string attemptPath = Path.Combine(analysisPath, AttemptsDN, attempt.ToStringInvariant());

        FileInfo definitionFile = new (Path.Combine(executionPath, DefinitionFN));
        if (definitionFile.Exists)
        {
            definitionFile.MoveTo(Path.Combine(attemptPath, DefinitionFN));
        }

        string payloadsPath = Path.Combine(analysisPath, InputPayloadsDN);
        foreach (FileInfo payloadFile in new DirectoryInfo(Path.Combine(executionPath, InputPayloadsDN)).EnumerateFiles())
        {
            payloadFile.MoveTo(Path.Combine(payloadsPath, payloadFile.Name));
        }

        return Task.CompletedTask;
    }

    public Task DeleteQueuedTreeAsync(Guid executionId)
    {
        DirectoryInfo directory = new (Path.Combine(rootPath, QueuedDN, executionId.ToString("D")));
        if (directory.Exists)
        {
            directory.Delete(true);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTreeAsync(AnalysisCoord coord)
    {
        (Guid analysisId, int attempt) = coord;

        DirectoryInfo analysisDirectory = new (Path.Combine(rootPath, analysisId.ToString("D")));
        DirectoryInfo attemptsDirectory = new (Path.Combine(analysisDirectory.FullName, AttemptsDN));
        DirectoryInfo attemptDirectory = new (Path.Combine(attemptsDirectory.FullName, attempt.ToStringInvariant()));
        if (attemptDirectory.Exists)
        {
            attemptDirectory.Delete(true);
        }

        if (analysisDirectory.Exists && (!attemptDirectory.Exists || !attemptsDirectory.EnumerateDirectories().Any()))
        {
            analysisDirectory.Delete(true);
        }

        return Task.CompletedTask;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    private sealed class Metadata
    {
        [JsonIgnore]
        public Encoding Encoding { get; }

        [JsonProperty("encoding")]
        private string Encoding0 => Encoding.WebName;

        public string? ContentType { get; }

        public string? Name { get; }

        public Metadata(EncodedStream encodedStream)
            : this(
                encodedStream.Encoding,
                (encodedStream as NamedEncodedStream)?.ContentType,
                (encodedStream as NamedEncodedStream)?.Name
            ) { }

        public Metadata(NamedEncodedStream encodedStream)
            : this(encodedStream.Encoding, encodedStream.ContentType, encodedStream.Name) { }

        [JsonConstructor]
        private Metadata(string encoding, string? contentType = null, string? name = null)
            : this(Encoding.GetEncoding(encoding), contentType, name) { }

        private Metadata(Encoding encoding, string? contentType = null, string? name = null)
        {
            Encoding = encoding;
            ContentType = contentType;
            Name = name;
        }
    }
}
