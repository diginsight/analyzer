using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business.Models;

internal record AnalyzerStepExecutorProto1(IAnalyzerStep Step, JObject Input) : IStepInstance
{
    StepMeta IStepInstance.Meta => Step.Meta;
}
