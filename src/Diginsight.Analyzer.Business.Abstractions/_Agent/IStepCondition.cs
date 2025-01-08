namespace Diginsight.Analyzer.Business;

public interface IStepCondition
{
    bool TryEvaluate(IAnalysisContextRO analysisContext, StepHistory stepHistory, out bool result);
}
