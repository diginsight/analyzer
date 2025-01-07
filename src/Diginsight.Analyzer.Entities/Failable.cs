using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class Failable : IFailable
{
    public bool IsFailed => Reason is not null;

    [DisallowNull]
    public Exception? Reason { get; private set; }

    public Failable() { }

    [JsonConstructor]
    public Failable(bool isFailed, Exception? reason)
    {
        if (isFailed && reason is not null)
        {
            Reason = reason;
        }
    }

    public void Fail(Exception reason)
    {
        Reason = Reason is { } reason0 ? new AggregateException(reason0, reason).Flatten() : reason;
    }

    public virtual bool IsSucceeded() => !IsFailed;

#if FEATURE_REPORTS
    public virtual Problem? ToProblem()
    {
        return Reason is { } reason ? Problem.Failed(reason) : null;
    }
#endif
}
