using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public interface IFailableRO : IAnalyzed
{
    bool IsFailed { get; }

    [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
    Exception? Reason { get; }
}
