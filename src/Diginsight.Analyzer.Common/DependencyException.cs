namespace Diginsight.Analyzer.Common;

public sealed class DependencyException<T> : ApplicationException
    where T : notnull
{
    public DependencyExceptionKind Kind { get; }

    public IReadOnlyCollection<T> Keys { get; }

    public DependencyException(DependencyExceptionKind kind, params T[] keys)
        : base(kind.ToString("G"))
    {
        Kind = kind;
        Keys = keys;
    }
}
