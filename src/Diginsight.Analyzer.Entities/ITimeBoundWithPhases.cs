using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBoundWithPhases : ITimeBoundWithPhasesRO, ITimeBound
{
    [DisallowNull]
    new DateTime? SetupFinishedAt { get; set; }

    DateTime? ITimeBoundWithPhasesRO.SetupFinishedAt => SetupFinishedAt;

    [DisallowNull]
    new DateTime? TeardownFinishedAt { get; set; }

    DateTime? ITimeBoundWithPhasesRO.TeardownFinishedAt => TeardownFinishedAt;
}
