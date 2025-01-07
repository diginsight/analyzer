using System.ComponentModel;

namespace Diginsight.Analyzer.Business.Models;

internal sealed class ActiveAgent : Agent
{
    public required ExecutionKind Kind { get; init; }

    public required Guid ExecutionId { get; init; }

    public required bool IsConflicting { get; init; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out bool isConflicting, out ExecutionKind kind, out Guid executionId)
    {
        isConflicting = IsConflicting;
        kind = Kind;
        executionId = ExecutionId;
    }
}
