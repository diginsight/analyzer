namespace Diginsight.Analyzer.Business.Models;

public readonly record struct ExtendedAnalysisCoord(Guid ExecutionId, Guid AnalysisId, int Attempt = 0) : IExecutionCoord
{
    ExecutionKind IExecutionCoord.Kind => ExecutionKind.Analysis;
    Guid IExecutionCoord.Id => ExecutionId;
}
