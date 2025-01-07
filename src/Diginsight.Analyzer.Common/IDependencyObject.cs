namespace Diginsight.Analyzer.Common;

public interface IDependencyObject<out T>
    where T : notnull
{
    T Key { get; }

    IEnumerable<T> Dependencies { get; }
}
