using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class CopyProgressAnalyzerStepExecutor : IAnalyzerStepExecutor
{
    private readonly CopyProgressStepInput input;
    private readonly ISnapshotService snapshotService;

    public StepMeta Meta { get; }
    public JObject RawInput { get; }
    public object ValidatedInput => input;
    public IStepCondition Condition { get; }

    public CopyProgressAnalyzerStepExecutor(
        StepMeta meta,
        JObject rawInput,
        CopyProgressStepInput validatedInput,
        IStepCondition condition,
        ISnapshotService snapshotService
    )
    {
        Meta = meta;
        RawInput = rawInput;
        input = validatedInput;
        Condition = condition;

        this.snapshotService = snapshotService;
    }

    public async Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken)
    {
        AnalysisContextSnapshot? snapshot;
        if (input.AnalysisId is { } analysisId)
        {
            AnalysisCoord coord = new (analysisId, input.Attempt ?? -1);
            snapshot = await snapshotService.GetAnalysisAsync(coord, true, cancellationToken);
        }
        else
        {
            snapshot = await snapshotService.GetAnalysisAsync(input.ExecutionId!.Value, true, cancellationToken);
        }

        if (snapshot is null)
        {
            analysisContext.GetStep(Meta.InternalName).Fail("NoSource");
            return;
        }

        JObject progress = analysisContext.Progress;
        foreach (JProperty jp in snapshot.Progress!.Properties())
        {
            progress[jp.Name] = jp.Value;
        }
    }
}
