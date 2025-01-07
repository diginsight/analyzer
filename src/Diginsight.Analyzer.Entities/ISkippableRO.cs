using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public interface ISkippableRO : IAnalyzed
{
    bool IsSkipped { get; }

    [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
    Exception? Reason { get; }
}
