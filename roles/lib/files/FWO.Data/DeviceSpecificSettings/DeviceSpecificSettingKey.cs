namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Identifies a device specific setting independently of its serialized value.
/// </summary>
public abstract class DeviceSpecificSettingKey
{
    private readonly HashSet<DeviceSpecificScopeType> allowedScopes;

    public string DatabaseKey { get; }

    public Type ValueType { get; }

    public IReadOnlySet<DeviceSpecificScopeType> AllowedScopes => allowedScopes;

    private protected DeviceSpecificSettingKey(
        string databaseKey,
        Type valueType,
        IEnumerable<DeviceSpecificScopeType> allowedScopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(allowedScopes);

        this.allowedScopes = allowedScopes.ToHashSet();
        if (this.allowedScopes.Count == 0 || this.allowedScopes.Contains(DeviceSpecificScopeType.Undefined))
        {
            throw new ArgumentException("A setting key must allow at least one defined scope.", nameof(allowedScopes));
        }

        DatabaseKey = databaseKey;
        ValueType = valueType;
    }

    /// <summary>Whether an override of this setting may be stored at the given scope type.</summary>
    public bool IsAllowedAt(DeviceSpecificScopeType scopeType)
    {
        return allowedScopes.Contains(scopeType);
    }

    public override string ToString()
    {
        return DatabaseKey;
    }
}

/// <summary>
/// Identifies a device specific setting and the C# type accepted for its value.
/// </summary>
public sealed class DeviceSpecificSettingKey<TValue> : DeviceSpecificSettingKey
{
    internal DeviceSpecificSettingKey(
        string databaseKey,
        IEnumerable<DeviceSpecificScopeType> allowedScopes)
        : base(databaseKey, typeof(TValue), allowedScopes)
    {
    }
}
