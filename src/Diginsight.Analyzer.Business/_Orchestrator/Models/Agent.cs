namespace Diginsight.Analyzer.Business.Models;

internal class Agent
{
    public required Uri BaseAddress { get; init; }

    public required string AgentName { get; init; }

    public required string AgentPool { get; init; }
}
