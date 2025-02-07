namespace Diginsight.Analyzer.Entities;

public readonly record struct ExecutionCoord(ExecutionKind Kind, Guid Id) : IExecutionCoord;
