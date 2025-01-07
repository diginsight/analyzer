namespace Diginsight.Analyzer.Business;

public interface IPlaceholderReplacer
{
    string Replace(string input, IAnalysisContextRO analysisContext, IStepHistoryRO step);
}
