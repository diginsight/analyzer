namespace Diginsight.Analyzer.Business;

internal interface ICompiler
{
    IStepCondition CompileCondition(StepMeta stepMeta);
}
