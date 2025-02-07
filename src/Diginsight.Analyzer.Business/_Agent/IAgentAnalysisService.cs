using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IAgentAnalysisService : IAnalysisService, IAsyncDisposable
{
    Task<ExtendedAnalysisCoord> AnalyzeAsync(
        GlobalMeta globalMeta,
        IEnumerable<IStepInstance> steps,
        JObject progress,
        EncodedStream definitionStream,
        IEnumerable<InputPayload> inputPayloads,
        CancellationToken cancellationToken
    );

    Task<ExtendedAnalysisCoord> ReattemptAsync(
        Guid analysisId,
        GlobalMeta? globalMeta,
        EncodedStream? definitionStream,
        CancellationToken cancellationToken
    );

    Task<AnalysisCoord> DequeueAsync(Guid executionId, CancellationToken cancellationToken);

    Task<ExtendedAnalysisCoord?> GetCurrentAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionsAE(CancellationToken cancellationToken);

    IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysesAE(Guid analysisId, CancellationToken cancellationToken);

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await AbortExecutionsAE(CancellationToken.None).ToArrayAsync();
    }
}
