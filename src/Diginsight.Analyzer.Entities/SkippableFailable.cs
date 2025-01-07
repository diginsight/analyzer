using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class SkippableFailable : IFailable, ISkippable
{
    public bool IsFailed { get; private set; }

    public bool IsSkipped => Reason is not null && !IsFailed;

    [DisallowNull]
    public Exception? Reason { get; private set; }

    public SkippableFailable() { }

    [JsonConstructor]
    public SkippableFailable(bool isFailed, bool isSkipped, Exception? reason)
    {
        if (((IsFailed = isFailed) || isSkipped) && reason is not null)
        {
            Reason = reason;
        }
    }

    public void Fail(Exception reason)
    {
        if (!IsFailed)
        {
            IsFailed = true;
            Reason = reason;
        }
        else
        {
            Reason = Reason is { } reason0 ? new AggregateException(reason0, reason).Flatten() : reason;
        }
    }

    public void Skip(Exception reason)
    {
        if (IsFailed)
            return;

        Reason = Reason is { } reason0 ? new AggregateException(reason0, reason).Flatten() : reason;
    }

    public virtual bool IsSucceeded() => !(IsFailed || IsSkipped);

#if FEATURE_REPORTS
    public virtual Problem? ToProblem()
    {
        return Reason is { } reason
            ? IsFailed ? Problem.Failed(reason) : Problem.Skipped(reason)
            : null;
    }
#endif
}
