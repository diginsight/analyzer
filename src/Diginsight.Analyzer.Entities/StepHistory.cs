using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public sealed class StepHistory : StepInstance, IStepHistory, ITimeBoundWithPhases
{
    private readonly SkippableFailable skippableFailable;

    [DisallowNull]
    public DateTime? SetupStartedAt { get; set; }

    [DisallowNull]
    public DateTime? SetupFinishedAt { get; set; }

    [DisallowNull]
    public DateTime? StartedAt { get; set; }

    [DisallowNull]
    public DateTime? FinishedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownStartedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownFinishedAt { get; set; }

    public TimeBoundStatus Status { get; set; }

    public bool IsFailed => skippableFailable.IsFailed;

    public bool IsSkipped => skippableFailable.IsSkipped;

    public Exception? Reason => skippableFailable.Reason;

    public StepHistory(IStepInstance stepInstance)
        : this(stepInstance.Meta, stepInstance.Input, false, false, null) { }

    [JsonConstructor]
    private StepHistory(
        StepMeta meta, JObject input, bool isFailed, bool isSkipped, Exception? reason
    )
        : base(meta, input)
    {
        skippableFailable = new SkippableFailable(isFailed, isSkipped, reason);
    }

    public void Fail(Exception reason) => skippableFailable.Fail(reason);

    public void Skip(Exception reason) => skippableFailable.Skip(reason);

    public bool IsSucceeded() => skippableFailable.IsSucceeded();

#if FEATURE_REPORTS
    public Problem? ToProblem() => skippableFailable.ToProblem();
#endif
}
