namespace Diginsight.Analyzer.Entities;

public interface IAnalyzed
{
    bool IsSucceeded();

#if FEATURE_REPORTS
    Problem? ToProblem();
#endif
}
