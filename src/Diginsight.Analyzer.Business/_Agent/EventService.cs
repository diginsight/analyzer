using Diginsight.Analyzer.Entities.Events;
using Microsoft.Extensions.Logging;

namespace Diginsight.Analyzer.Business;

internal sealed class EventService : IEventService
{
    private readonly ILogger logger;
    private readonly TimeProvider timeProvider;

    public EventService(
        ILogger<EventService> logger,
        TimeProvider timeProvider
    )
    {
        this.logger = logger;
        this.timeProvider = timeProvider;
    }

    public async Task EmitAsync(IEnumerable<IEventSender> eventSenders, Func<DateTime, Event> makeEvent)
    {
        Event @event = makeEvent(timeProvider.GetUtcNow().UtcDateTime);

        foreach (IEventSender eventSender in eventSenders)
        {
            try
            {
                await eventSender.SendAsync(@event);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "{EventSender} failed to send event", eventSender.GetType().Name);
            }
        }
    }
}
