using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Diginsight.Analyzer.Common;

public static class CommonUtils
{
    private static bool? isAgent;

    public static readonly Encoding DefaultEncoding = new UTF8Encoding(false);

    public static bool IsAgent => isAgent ??= ToBool(Environment.GetEnvironmentVariable("ANALYZER_ORCHESTRATOR")) != true;

    public static TObj[] SortByDependency<TObj, TKey>(
        IEnumerable<TObj> depObjects,
        IEnumerable<TKey>? desiredKeys = null,
        IEqualityComparer<TKey>? equalityComparer = null,
        bool throwOnUnknownKey = true
    )
        where TObj : IDependencyObject<TKey>
        where TKey : notnull
    {
        return CoreSortByDependency().ToArray();

        IEnumerable<TObj> CoreSortByDependency()
        {
            IReadOnlyDictionary<TKey, TObj> objectDict = depObjects.ToDictionary(static x => x.Key, equalityComparer);
            ISet<TKey> pendingKeys = new HashSet<TKey>(desiredKeys ?? objectDict.Keys, equalityComparer);
            ISet<TKey> seenKeys = new HashSet<TKey>(equalityComparer);

            while (pendingKeys.Any())
            {
                bool resume = false;

                foreach (TKey pendingKey in pendingKeys)
                {
                    if (!objectDict.TryGetValue(pendingKey, out TObj? depObject))
                    {
                        if (throwOnUnknownKey)
                        {
                            throw new DependencyException<TKey>(DependencyExceptionKind.UnknownObject, pendingKey);
                        }

                        seenKeys.Add(pendingKey);
                        pendingKeys.Remove(pendingKey);

                        resume = true;
                        break;
                    }

                    IEnumerable<TKey> dependencies = depObject.Dependencies;
                    TKey[] unknownDependencies = dependencies.Except(objectDict.Keys, equalityComparer).ToArray();
                    if (unknownDependencies.Length > 0)
                    {
                        if (throwOnUnknownKey)
                        {
                            throw new DependencyException<TKey>(DependencyExceptionKind.UnknownObjectDependencies, unknownDependencies);
                        }

                        seenKeys.UnionWith(unknownDependencies);
                    }

                    if (seenKeys.IsSupersetOf(dependencies))
                    {
                        seenKeys.Add(pendingKey);
                        pendingKeys.Remove(pendingKey);
                        yield return depObject;

                        resume = true;
                        break;
                    }

                    IEnumerable<TKey> unseenDependencies = dependencies.Except(seenKeys, equalityComparer).ToArray();
                    if (!pendingKeys.IsSupersetOf(unseenDependencies))
                    {
                        pendingKeys.UnionWith(unseenDependencies);
                        resume = true;
                        break;
                    }
                }

                if (!resume)
                {
                    throw new DependencyException<TKey>(DependencyExceptionKind.CircularDependency);
                }
            }
        }
    }

    public static bool? ToBool(string? str)
    {
        return str is null ? null
            : int.TryParse(str, out int i) ? i != 0
            : bool.TryParse(str, out bool b) ? b
            : null;
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static string? GetEnvironmentVariable(string variable, string? defaultValue = null)
    {
        return Environment.GetEnvironmentVariable(variable)?.Trim() is not (null or "") and var value ? value : defaultValue;
    }
}
