namespace Diginsight.Analyzer.Business;

internal class CompilerFactory : ICompilerFactory
{
    public ICompiler Make() => new Compiler();
}
