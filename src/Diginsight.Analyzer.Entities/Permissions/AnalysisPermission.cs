using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

[JsonConverter(typeof(IPermission<AnalysisPermission>.Converter))]
public readonly struct AnalysisPermission : IPermission<AnalysisPermission>
{
    public static readonly AnalysisPermission None = default;
    public static readonly AnalysisPermission Start = new (nameof(Start), false, true, false);
    public static readonly AnalysisPermission Read = new (nameof(Read), false, true, false);
    public static readonly AnalysisPermission ReadAndExecute = new (nameof(ReadAndExecute), false, true, true);

    static IReadOnlyDictionary<string, AnalysisPermission> IPermission<AnalysisPermission>.Values { get; } =
        new Dictionary<string, AnalysisPermission>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(None)] = None,
            [nameof(Start)] = Start,
            [nameof(Read)] = Read,
            [nameof(ReadAndExecute)] = ReadAndExecute,
        };

    // ReSharper disable once ReplaceWithFieldKeyword
    private readonly string? name;

    string IPermission.Name => name ?? nameof(None);

    public bool CanStart { get; }

    public bool CanRead { get; }

    public bool CanExecute { get; }

    private AnalysisPermission(string name, bool canStart, bool canRead, bool canExecute)
    {
        this.name = name;
        CanStart = canStart;
        CanRead = canRead;
        CanExecute = canExecute;
    }

    public bool Equals(AnalysisPermission other)
    {
        return name == other.name;
    }

    public override bool Equals(object? obj)
    {
        return obj is AnalysisPermission other && Equals(other);
    }

    public override int GetHashCode()
    {
        return name != null ? name.GetHashCode() : 0;
    }

    public static bool operator ==(AnalysisPermission left, AnalysisPermission right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AnalysisPermission left, AnalysisPermission right)
    {
        return !left.Equals(right);
    }

    public static bool operator >> (AnalysisPermission left, IPermission right)
    {
        return right is AnalysisPermission other
            && (left.CanStart || !other.CanStart)
            && (left.CanRead || !other.CanRead)
            && (left.CanExecute || !other.CanExecute);
    }
}
