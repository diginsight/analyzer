namespace Diginsight.Analyzer.Business.Models;

public abstract class ExecutionContext : Failable, IExecutionContext
{
    public ExecutionCoord ExecutionCoord { get; }

    protected ExecutionContext(ExecutionCoord executionCoord)
    {
        ExecutionCoord = executionCoord;
    }

    public abstract bool IsNotStarted();
}
