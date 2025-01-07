﻿namespace Diginsight.Analyzer.Entities.Events;

public abstract class Event
{
    public abstract EventKind EventKind { get; }

    public required ExecutionCoord ExecutionCoord { get; init; }

    public required DateTime Timestamp { get; init; }

    public required EventRecipientInput RecipientInput { get; init; }
}
