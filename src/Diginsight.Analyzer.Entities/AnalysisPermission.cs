using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(IPermission<AnalysisPermission>.Converter))]
public readonly struct AnalysisPermission : IPermission<AnalysisPermission>
{
    public static readonly AnalysisPermission None = default;
    public static readonly AnalysisPermission Read = new (nameof(Read), true, false);
    public static readonly AnalysisPermission Execute = new (nameof(Execute), false, true);
    public static readonly AnalysisPermission ReadAndExecute = new (nameof(ReadAndExecute), true, true);

    static IReadOnlyDictionary<string, AnalysisPermission> IPermission<AnalysisPermission>.Values { get; } =
        new Dictionary<string, AnalysisPermission>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(None)] = None,
            [nameof(Read)] = Read,
            [nameof(Execute)] = Execute,
            [nameof(ReadAndExecute)] = ReadAndExecute,
        };

    private readonly string? name;

    string IPermission<AnalysisPermission>.Name => name ?? nameof(None);

    public bool CanRead { get; }

    public bool CanExecute { get; }

    private AnalysisPermission(string name, bool canRead, bool canExecute)
    {
        this.name = name;
        CanRead = canRead;
        CanExecute = canExecute;
    }
}
