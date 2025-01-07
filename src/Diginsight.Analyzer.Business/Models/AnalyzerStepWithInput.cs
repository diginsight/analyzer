using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal readonly record struct AnalyzerStepWithInput(IAnalyzerStep Step, JObject Input);
