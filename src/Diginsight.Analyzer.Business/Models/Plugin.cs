namespace Diginsight.Analyzer.Business.Models;

public readonly record struct Plugin(Guid Id, bool IsDefault, IEnumerable<string> AnalyzerStepTemplates);
