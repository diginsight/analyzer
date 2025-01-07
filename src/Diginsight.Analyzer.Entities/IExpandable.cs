namespace Diginsight.Analyzer.Entities;

public interface IExpandable<in T>
    where T : Expandable<T>, new()
{
    TExpansion As<TExpansion>()
        where TExpansion : T, new();
}
