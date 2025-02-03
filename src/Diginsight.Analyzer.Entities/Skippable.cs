using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class Skippable : ISkippable
{
    public bool IsSkipped => Reason is not null;

    [DisallowNull]
    public Exception? Reason { get; private set; }

    public Skippable() { }

    [JsonConstructor]
    public Skippable(bool isSkipped, Exception? reason)
    {
        if (isSkipped && reason is not null)
        {
            Reason = reason;
        }
    }

    public void Skip(Exception reason)
    {
        Reason = Reason is { } reason0 ? new AggregateException(reason0, reason).Flatten() : reason;
    }

    public virtual bool IsSucceeded() => !IsSkipped;

#if FEATURE_REPORTS
    public virtual Problem? ToProblem()
    {
        return Reason is { } reason ? Problem.Skipped(reason) : null;
    }
#endif
}
