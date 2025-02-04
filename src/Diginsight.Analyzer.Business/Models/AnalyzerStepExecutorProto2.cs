using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal sealed record AnalyzerStepExecutorProto2(IAnalyzerStep Step, JObject RawInput, object ValidatedInput, IStepCondition Condition)
    : AnalyzerStepExecutorProto1(Step, RawInput);
