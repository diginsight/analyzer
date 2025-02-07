namespace Diginsight.Analyzer.Business.Models;

internal interface IAgentAnalysisContext : IAnalysisContext, ITimeBound
{
    new IEnumerable<StepHistory> Steps { get; }

    IEnumerable<IStepHistoryRO> IAnalysisContextRO.Steps => Steps;

    new StepHistory GetStep(string internalName);

    IStepHistoryRO IAnalysisContextRO.GetStep(string internalName) => GetStep(internalName);
}
