using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal sealed record AnalyzerStepExecutorProto2(IAnalyzerStep Step, JObject Input, IStepCondition Condition)
    : AnalyzerStepExecutorProto1(Step, Input);
