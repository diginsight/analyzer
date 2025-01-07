using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBound
{
    [DisallowNull]
    DateTime? FinishedAt { get; set; }

    TimeBoundStatus Status { get; set; }
}
