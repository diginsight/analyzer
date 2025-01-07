namespace Diginsight.Analyzer.Business;

internal interface IEvaluator
{
    bool TryEvalCondition(StepHistory stepHistory, out bool enabled);
}
