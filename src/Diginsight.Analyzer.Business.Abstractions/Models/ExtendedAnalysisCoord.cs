namespace Diginsight.Analyzer.Business.Models;

public readonly record struct ExtendedAnalysisCoord(Guid ExecutionId, Guid AnalysisId, int Attempt = 0) : IExtendedAnalysisCoord;
