using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public sealed class SkipReason : ApplicationException
{
    [JsonConstructor]
    public SkipReason(string message)
        : base(message) { }
}
