namespace Diginsight.Analyzer.Business.Configurations;

public interface IOrchestratorCoreOptions : ICoreOptions
{
    int AgentTimeoutSeconds { get; }

    int DequeuerIntervalSeconds { get; }

    int DequeuerMaxFailures { get; }

    bool AllowAllEventsNotification { get; }
}
