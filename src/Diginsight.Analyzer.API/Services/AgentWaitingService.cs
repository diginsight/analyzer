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

    private ManualResetEventSlim GetMre(Guid executionId) => memoryCache.GetOrCreate(executionId, static _ => new ManualResetEventSlim())!;

    private void SetMre(Guid executionId)
    {
        ManualResetEventSlim mre = memoryCache.Get<ManualResetEventSlim>(executionId)!;
        mre.Set();
        memoryCache.Set(executionId, mre, TimeSpan.FromMinutes(5));
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
                    _ = owner.GetMre(@event.ExecutionCoord.Id);
                    break;
                }

                case EventKind.AnalysisFinished:
                {
                    owner.SetMre(@event.ExecutionCoord.Id);
                    break;
                }
            }

            return Task.CompletedTask;
        }
    }
}
