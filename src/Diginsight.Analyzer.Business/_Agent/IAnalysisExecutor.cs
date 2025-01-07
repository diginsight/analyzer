using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

internal interface IAnalysisExecutor
{
    Task ExecuteAsync(
        Guid executionId,
        AnalysisCoord coord,
        DateTime? queuedAt,
        GlobalMeta globalMeta,
        IEnumerable<IAnalyzerStepExecutor> stepExecutors,
        IEnumerable<IEventSender> eventSenders,
        JObject progress,
        CancellationToken cancellationToken
    );
}
