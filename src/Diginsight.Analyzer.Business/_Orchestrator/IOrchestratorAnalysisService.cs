using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IOrchestratorAnalysisService : IAnalysisService
{
    Task<QueuableAnalysisCoord> AnalyzeAsync(
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        EncodedStream definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        string? agentPool,
        QueuingPolicy queuingPolicy,
        CancellationToken cancellationToken
    );

    Task<QueuableAnalysisCoord> ReattemptAsync(
        Guid analysisId,
        GlobalMeta? globalMeta,
        EncodedStream? definitionStream,
        string? agentPool,
        QueuingPolicy queuingPolicy,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionsAE(Selection selection, CancellationToken cancellationToken);

    IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysesAE(Guid analysisId, Selection selection, CancellationToken cancellationToken);
}
