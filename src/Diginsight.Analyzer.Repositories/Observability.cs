using System.Diagnostics;

namespace Diginsight.Analyzer.Repositories;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new (typeof(Observability).Namespace!);
}
