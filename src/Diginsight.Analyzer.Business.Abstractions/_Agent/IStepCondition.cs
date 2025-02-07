namespace Diginsight.Analyzer.Business;

public interface IStepCondition
{
    bool TryEvaluate(IAnalysisContextRO analysisContext, IStepHistory stepHistory, out bool result);
}
