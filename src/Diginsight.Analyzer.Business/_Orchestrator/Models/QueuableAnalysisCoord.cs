namespace Diginsight.Analyzer.Business.Models;

public readonly record struct QueuableAnalysisCoord(Guid ExecutionId, Guid AnalysisId, int Attempt, bool Queued) : IExtendedAnalysisCoord;
