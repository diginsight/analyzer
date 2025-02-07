namespace Diginsight.Analyzer.Business.Models;

public readonly record struct QueuableAnalysisCoord(Guid ExecutionId, Guid AnalysisId, int Attempt, bool Queued) : IExecutionCoord
{
    ExecutionKind IExecutionCoord.Kind => ExecutionKind.Analysis;
    Guid IExecutionCoord.Id => ExecutionId;
}
