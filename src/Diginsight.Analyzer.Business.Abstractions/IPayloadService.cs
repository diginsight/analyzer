using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public interface IPayloadService
{
    public const string DefinitionContentType = IAnalysisFileRepository.DefinitionContentType;

    Task<NamedEncodedStream?> ReadPayloadAsync(Guid executionId, string label, CancellationToken cancellationToken);

    Task<NamedEncodedStream?> ReadPayloadAsync(AnalysisCoord coord, string label, CancellationToken cancellationToken);

    Task<EncodedStream?> ReadDefinitionAsync(Guid executionId, CancellationToken cancellationToken);

    Task<EncodedStream?> ReadDefinitionAsync(AnalysisCoord coord, CancellationToken cancellationToken);

    Task<IAsyncEnumerable<PayloadDescriptor>?> GetPayloadDescriptorsAsync(Guid executionId, CancellationToken cancellationToken);

    Task<IAsyncEnumerable<PayloadDescriptor>?> GetPayloadDescriptorsAsync(AnalysisCoord analysisCoord, CancellationToken cancellationToken);
}
