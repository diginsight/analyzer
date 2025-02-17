using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(IPermission<AnalysisPermission>.Converter))]
public readonly struct AnalysisPermission : IPermission<AnalysisPermission>
{
    public static readonly AnalysisPermission None = default;
    public static readonly AnalysisPermission Start = new (nameof(Start), false, true, false);
    public static readonly AnalysisPermission Read = new (nameof(Read), false, true, false);
    public static readonly AnalysisPermission Execute = new (nameof(Execute), false, false, true);
    public static readonly AnalysisPermission ReadAndExecute = new (nameof(ReadAndExecute), false, true, true);

    static IReadOnlyDictionary<string, AnalysisPermission> IPermission<AnalysisPermission>.Values { get; } =
        new Dictionary<string, AnalysisPermission>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(None)] = None,
            [nameof(Start)] = Start,
            [nameof(Read)] = Read,
            [nameof(Execute)] = Execute,
            [nameof(ReadAndExecute)] = ReadAndExecute,
        };

    // ReSharper disable once ReplaceWithFieldKeyword
    private readonly string? name;

    string IPermission<AnalysisPermission>.Name => name ?? nameof(None);

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
}
