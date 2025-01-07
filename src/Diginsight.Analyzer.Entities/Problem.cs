#if FEATURE_REPORTS
namespace Diginsight.Analyzer.Entities;

public sealed class Problem
{
    public ProblemKind Kind { get; }

    public Exception Reason { get; }

    private Problem(ProblemKind kind, Exception reason)
    {
        Kind = kind;
        Reason = reason;
    }

    public static Problem Failed(Exception reason) => new (ProblemKind.Failed, reason);

    public static Problem Skipped(Exception reason) => new (ProblemKind.Skipped, reason);
}
#endif
