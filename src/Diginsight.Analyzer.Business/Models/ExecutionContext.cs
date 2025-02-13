namespace Diginsight.Analyzer.Business.Models;

internal abstract class ExecutionContext : Failable, IExecutionContext
{
    public ExecutionCoord ExecutionCoord { get; }

    protected ExecutionContext(ExecutionCoord executionCoord)
    {
        ExecutionCoord = executionCoord;
    }
}
