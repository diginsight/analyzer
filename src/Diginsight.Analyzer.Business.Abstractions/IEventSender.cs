﻿using Diginsight.Analyzer.Entities.Events;

namespace Diginsight.Analyzer.Business;

public interface IEventSender
{
    Task SendAsync(Event @event);
}
