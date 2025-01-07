using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public interface IAgentPayloadService : IPayloadService
{
    Task<bool> TryWritePayloadAsync(AnalysisCoord coord, string label, NamedEncodedStream encodedStream, CancellationToken cancellationToken);
}
