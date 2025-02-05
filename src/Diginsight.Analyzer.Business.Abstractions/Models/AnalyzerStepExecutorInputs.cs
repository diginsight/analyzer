using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

public sealed record AnalyzerStepExecutorInputs(JObject Raw, object Validated, IStepCondition Condition);
