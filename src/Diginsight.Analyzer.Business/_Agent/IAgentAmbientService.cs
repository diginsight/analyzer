namespace Diginsight.Analyzer.Business;

public interface IAgentAmbientService : IAmbientService
{
    Uri BaseAddress { get; }

    string AgentName { get; }

    string AgentPool { get; }
}
