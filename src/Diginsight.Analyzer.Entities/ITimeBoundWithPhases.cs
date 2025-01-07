using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBoundWithPhases : ITimeBound
{
    [DisallowNull]
    DateTime? SetupFinishedAt { get; set; }

    [DisallowNull]
    DateTime? TeardownFinishedAt { get; set; }
}
