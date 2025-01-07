namespace Diginsight.Analyzer.Repositories.Models;

public readonly record struct Page<T>(IEnumerable<T> Items, int TotalCount);
