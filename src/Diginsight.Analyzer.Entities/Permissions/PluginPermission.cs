using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

[JsonConverter(typeof(IPermission<PluginPermission>.Converter))]
public readonly struct PluginPermission : IPermission<PluginPermission>
{
    public static readonly PluginPermission None = default;
    public static readonly PluginPermission Manage = new (nameof(Manage), true);

    static IReadOnlyDictionary<string, PluginPermission> IPermission<PluginPermission>.Values { get; } =
        new Dictionary<string, PluginPermission>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(None)] = None,
            [nameof(Manage)] = Manage,
        };

    // ReSharper disable once ReplaceWithFieldKeyword
    private readonly string? name;

    string IPermission.Name => name ?? nameof(None);

    public bool CanManage { get; }

    private PluginPermission(string name, bool canManage)
    {
        this.name = name;
        CanManage = canManage;
    }

    public bool Equals(PluginPermission other)
    {
        return name == other.name;
    }

    public override bool Equals(object? obj)
    {
        return obj is PluginPermission other && Equals(other);
    }

    public override int GetHashCode()
    {
        return name != null ? name.GetHashCode() : 0;
    }

    public static bool operator ==(PluginPermission left, PluginPermission right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PluginPermission left, PluginPermission right)
    {
        return !left.Equals(right);
    }

    public static bool operator >> (PluginPermission left, IPermission right)
    {
        return right is PluginPermission other
            && (left.CanManage || !other.CanManage);
    }
}
