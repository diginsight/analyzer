namespace Diginsight.Analyzer.Business;

internal class EvaluatorFactory : IEvaluatorFactory
{
    public IEvaluator Make(IAnalysisContextRO analysisContext) => new Evaluator(analysisContext);
}
