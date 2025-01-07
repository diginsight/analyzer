using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public sealed class FailReason : ApplicationException
{
    [JsonConstructor]
    public FailReason(string message)
        : base(message) { }
}
