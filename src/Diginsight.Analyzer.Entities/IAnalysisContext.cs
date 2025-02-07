using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContext : IAnalysisContextRO, IExecutionContext
{
    JObject Progress { get; }
}
