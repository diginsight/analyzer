namespace Diginsight.Analyzer.Business.Configurations;

public sealed class CoreOptions : IAgentCoreOptions, IOrchestratorCoreOptions
{
    public int DefaultParallelism { get; set; } = 4;

    public int DefaultPageSize { get; set; } = 10;

    public int MaxPageSize { get; set; } = 100;

    public string DefaultAgentPool { get; set; } = "prod";

    public int LeaseTtlMinutes { get; set; } = 5;

    public int AgentTimeoutSeconds { get; set; } = 10;

    public int DequeuerIntervalSeconds { get; set; } = 30;

    public int DequeuerMaxFailures { get; set; } = 5;

    public bool AllowAllEventsNotification { get; set; }
}
