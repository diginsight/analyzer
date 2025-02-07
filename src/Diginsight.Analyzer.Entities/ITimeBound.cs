using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBound : ITimeBoundRO
{
    [DisallowNull]
    new DateTime? FinishedAt { get; set; }

    DateTime? ITimeBoundRO.FinishedAt => FinishedAt;

    new TimeBoundStatus Status { get; set; }

    TimeBoundStatus ITimeBoundRO.Status => Status;
}
