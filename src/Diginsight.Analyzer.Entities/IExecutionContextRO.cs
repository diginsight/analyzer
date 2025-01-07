namespace Diginsight.Analyzer.Entities;

public interface IExecutionContextRO : IFailableRO
{
    ExecutionCoord ExecutionCoord { get; }

    bool IsNotStarted();
}
