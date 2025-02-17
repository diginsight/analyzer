using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(IPermission<PermissionPermission>.Converter))]
public readonly struct PermissionPermission : IPermission<PermissionPermission>
{
    public static readonly PermissionPermission None = default;
    public static readonly PermissionPermission Manage = new (nameof(Manage), true);

    static IReadOnlyDictionary<string, PermissionPermission> IPermission<PermissionPermission>.Values { get; } =
        new Dictionary<string, PermissionPermission>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(None)] = None,
            [nameof(Manage)] = Manage,
        };

    // ReSharper disable once ReplaceWithFieldKeyword
    private readonly string? name;

    string IPermission<PermissionPermission>.Name => name ?? nameof(None);

    public bool CanManage { get; }

    private PermissionPermission(string name, bool canManage)
    {
        this.name = name;
        CanManage = canManage;
    }
}
