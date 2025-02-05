namespace Diginsight.Analyzer.Business.Models;

internal sealed record AnalyzerStepExecutorProto2(IAnalyzerStep Step, AnalyzerStepExecutorInputs Inputs)
    : AnalyzerStepExecutorProto1(Step, Inputs.Raw);
