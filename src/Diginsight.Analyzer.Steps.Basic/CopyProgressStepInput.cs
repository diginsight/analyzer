using Newtonsoft.Json;

namespace Diginsight.Analyzer.Steps;

[JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
internal sealed class CopyProgressStepInput
{
    public Guid? AnalysisId { get; init; }
    public int? Attempt { get; init; }
    public Guid? ExecutionId { get; init; }
}
