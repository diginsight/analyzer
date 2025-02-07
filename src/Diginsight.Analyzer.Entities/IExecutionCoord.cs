namespace Diginsight.Analyzer.Entities;

public interface IExecutionCoord
{
    ExecutionKind Kind { get; }
    Guid Id { get; }
}
