using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

internal interface IAgentClient
{
    Task<ExtendedAnalysisCoord> AnalyzeAsync(
        EncodedStream definitionStream, EncodedStream progressStream, IEnumerable<InputPayload> inputPayloads, CancellationToken cancellationToken
    );

    Task<ExtendedAnalysisCoord> DequeueAsync(Guid executionId, CancellationToken cancellationToken);

    Task<ExtendedAnalysisCoord> ReattemptAsync(Guid analysisId, EncodedStream? definitionStream, CancellationToken cancellationToken);

    Task<IEnumerable<ExtendedAnalysisCoord>> AbortExecutionsAsync(Guid? executionId, CancellationToken cancellationToken);

    Task<IEnumerable<ExtendedAnalysisCoord>> AbortAnalysesAsync(Guid analysisId, int? attempt, CancellationToken cancellationToken);
}
