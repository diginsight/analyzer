using Newtonsoft.Json.Linq;
#if FEATURE_REPORTS
using Diginsight.Analyzer.Business.Models;
#endif

namespace Diginsight.Analyzer.Business;

public interface IAnalyzerStep
{
    IAnalyzerStepTemplate Template { get; }

    StepMeta Meta { get; }

    Task<JObject> ValidateAsync(JObject stepInput, CancellationToken cancellationToken);

    Task<bool> HasConflictAsync(IEnumerable<StepInstance> steps, AnalysisLease lease, CancellationToken cancellationToken) => Task.FromResult(false);

    IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, JObject input, IStepCondition condition);

#if FEATURE_REPORTS
    StepReport GetReport(TimeBoundStatus status, JObject progress) => new (Meta.InternalName, status);

    protected StepReport GetReport(TimeBoundStatus status, IEnumerable<KeyValuePair<object, IAnalyzed>> analyzedItems)
    {
        return new StepReport(
            Meta.InternalName,
            status,
            analyzedItems
                .Select(static x => (x.Key, Problem: x.Value.ToProblem()))
                .Where(static x => x.Problem is not null)
                .ToDictionary(static x => x.Key, static x => x.Problem!)
        );
    }

    protected StepReport GetReport<TItem>(TimeBoundStatus status, IEnumerable<KeyValuePair<object, TItem>> analyzedItems)
        where TItem : IAnalyzed
    {
        return GetReport(status, analyzedItems.Select(static x => KeyValuePair.Create(x.Key, (IAnalyzed)x.Value)));
    }
#endif
}
