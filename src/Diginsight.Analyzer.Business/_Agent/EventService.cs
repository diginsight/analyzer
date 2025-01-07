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

    public async Task EmitAsync(
        IEnumerable<IEventSender> eventSenders, IEnumerable<EventRecipient> eventRecipients, Func<EventRecipientInput, DateTime, Event> makeEvent
    )
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        IEnumerable<Event> events = eventRecipients.Select(er => makeEvent(er.Input, utcNow)).ToArray();

        foreach (IEventSender eventSender in eventSenders)
        {
            try
            {
                await eventSender.SendAsync(events);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "{EventSender} failed to send events", eventSender.GetType().Name);
            }
        }
    }
}
