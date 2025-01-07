namespace Diginsight.Analyzer.Business.Configurations;

public interface IAgentCoreOptions : ICoreOptions
{
    int LeaseTtlMinutes { get; }
}
