using Diginsight.Analyzer.Entities.Events;

namespace Diginsight.Analyzer.Business;

public interface IEventService
{
    Task EmitAsync(IEnumerable<IEventSender> eventSenders, Func<DateTime, Event> makeEvent);
}
