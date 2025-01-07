using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

public interface IAnalysisService
{
    IAsyncEnumerable<ExtendedAnalysisCoord> AbortExecutionAE(Guid executionId, CancellationToken cancellationToken);

    IAsyncEnumerable<ExtendedAnalysisCoord> AbortAnalysisAE(AnalysisCoord coord, CancellationToken cancellationToken);
}
