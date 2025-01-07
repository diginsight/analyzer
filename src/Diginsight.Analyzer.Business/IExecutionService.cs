namespace Diginsight.Analyzer.Business;

internal interface IExecutionService
{
    IAsyncEnumerable<(Guid Id, object Detail)> AbortAE(ExecutionKind kind, Guid? executionId, CancellationToken cancellationToken);
}
