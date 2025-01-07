using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Repositories;

public interface IAnalysisFileRepository
{
    public const string DefinitionContentType = "application/yaml";

    Task WriteDefinitionAsync(EncodedStream encodedStream, AnalysisCoord coord, CancellationToken cancellationToken);

    Task WriteQueuedDefinitionAsync(EncodedStream encodedStream, Guid executionId, CancellationToken cancellationToken);

    Task<EncodedStream> ReadDefinitionAsync(AnalysisCoord coord, CancellationToken cancellationToken);

    Task WriteProgressAsync(JObject progress, AnalysisCoord coord, CancellationToken cancellationToken);

    Task<JObject?> ReadProgressAsync(AnalysisCoord coord, CancellationToken cancellationToken);

    Task WriteInputPayloadAsync(NamedEncodedStream encodedStream, Guid analysisId, string label, CancellationToken cancellationToken);

    Task WriteQueuedInputPayloadAsync(NamedEncodedStream encodedStream, Guid executionId, string label, CancellationToken cancellationToken);

    Task WriteOutputPayloadAsync(NamedEncodedStream encodedStream, AnalysisCoord coord, string label, bool temporary, CancellationToken cancellationToken);

    Task<NamedEncodedStream?> ReadPayloadAsync(AnalysisCoord coord, string label, CancellationToken cancellationToken);

    IAsyncEnumerable<PayloadDescriptor> GetPayloadDescriptorsAE(AnalysisCoord coord, CancellationToken cancellationToken);

    Task MoveQueuedTreeAsync(Guid executionId, AnalysisCoord coord, CancellationToken cancellationToken);

    Task DeleteQueuedTreeAsync(Guid executionId);

    Task DeleteTreeAsync(AnalysisCoord coord);
}
