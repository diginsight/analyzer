using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities.Events;
using Microsoft.Extensions.Caching.Memory;

namespace Diginsight.Analyzer.API.Services;

internal sealed class AgentWaitingService : IWaitingService
{
    private readonly IMemoryCache memoryCache;

    public AgentWaitingService(ILoggerFactory loggerFactory)
    {
        memoryCache = new MemoryCache(new MemoryCacheOptions(), loggerFactory);
    }

    public Task WaitAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return Task.Run(() => GetMre(executionId).Wait(cancellationToken), cancellationToken);
    }

    private ManualResetEventSlim GetMre(Guid executionId) => memoryCache.Get<ManualResetEventSlim>(executionId)!;

    private void SetMre(Guid executionId, ManualResetEventSlim mre, TimeSpan? absoluteExpiration = null)
    {
        if (absoluteExpiration is { } absoluteExpiration0)
            memoryCache.Set(executionId, mre, absoluteExpiration0);
        else
            memoryCache.Set(executionId, mre);
    }

    public sealed class EventSender : IEventSender
    {
        private readonly AgentWaitingService owner;

        public EventSender(AgentWaitingService owner)
        {
            this.owner = owner;
        }

        public Task SendAsync(Event @event)
        {
            switch (@event.EventKind)
            {
                case EventKind.AnalysisStarted:
                {
                    Guid executionId = @event.ExecutionCoord.Id;

                    ManualResetEventSlim mre = new ();
                    owner.SetMre(executionId, mre);

                    break;
                }

                case EventKind.AnalysisFinished:
                {
                    Guid executionId = @event.ExecutionCoord.Id;

                    ManualResetEventSlim mre = owner.GetMre(executionId)!;
                    mre.Set();
                    owner.SetMre(executionId, mre, TimeSpan.FromMinutes(5));

                    break;
                }
            }

            return Task.CompletedTask;
        }
    }
}
