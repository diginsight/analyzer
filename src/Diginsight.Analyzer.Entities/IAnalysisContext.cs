namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContext : IAnalysisContextRO, IExecutionContext
{
    new IEnumerable<StepHistory> Steps { get; }

    IEnumerable<IStepHistoryRO> IAnalysisContextRO.Steps => Steps;

    new StepHistory GetStep(string internalName);

    IStepHistoryRO IAnalysisContextRO.GetStep(string internalName) => GetStep(internalName);
}
