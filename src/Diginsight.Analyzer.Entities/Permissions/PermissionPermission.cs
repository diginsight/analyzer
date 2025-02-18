﻿using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

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

    string IPermission.Name => name ?? nameof(None);

    public bool CanManage { get; }

    private PermissionPermission(string name, bool canManage)
    {
        this.name = name;
        CanManage = canManage;
    }

    public bool Equals(PermissionPermission other)
    {
        return name == other.name;
    }

    public override bool Equals(object? obj)
    {
        return obj is PermissionPermission other && Equals(other);
    }

    public override int GetHashCode()
    {
        return name != null ? name.GetHashCode() : 0;
    }

    public static bool operator ==(PermissionPermission left, PermissionPermission right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PermissionPermission left, PermissionPermission right)
    {
        return !left.Equals(right);
    }

    public static bool operator >> (PermissionPermission left, IPermission right)
    {
        return right is PermissionPermission other
            && (left.CanManage || !other.CanManage);
    }
}
