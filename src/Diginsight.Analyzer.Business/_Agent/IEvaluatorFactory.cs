namespace Diginsight.Analyzer.Business;

internal interface IEvaluatorFactory
{
    IEvaluator Make(IAnalysisContextRO analysisContext);
}
