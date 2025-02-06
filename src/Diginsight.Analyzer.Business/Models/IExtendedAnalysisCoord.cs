namespace Diginsight.Analyzer.Business.Models;

public interface IExtendedAnalysisCoord
{
    Guid ExecutionId { get; }
    Guid AnalysisId { get; }
    int Attempt { get; }
}
