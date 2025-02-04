using Newtonsoft.Json;

namespace Diginsight.Analyzer.Steps;

internal static class DelayAnalyzerStepInput
{
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    public sealed class Raw
    {
        public string? Delay { get; init; }
        public double? DelaySeconds { get; init; }
        public int? DelayMilliseconds { get; init; }
    }

    public sealed record Validated(TimeSpan Delay);
}
