namespace Diginsight.Analyzer.Business;

public interface IAnalyzerStepTemplate
{
    string Name { get; }

    IAnalyzerStep Create(StepMeta meta);
}
