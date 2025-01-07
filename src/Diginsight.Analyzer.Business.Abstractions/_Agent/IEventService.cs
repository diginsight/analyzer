using Diginsight.Analyzer.Entities.Events;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IEventService
{
    Task EmitAsync(
        IEnumerable<IEventSender> eventSenders, IEnumerable<EventRecipient> eventRecipients, Func<JObject, DateTime, Event> makeEvent
    );
}
