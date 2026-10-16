namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// A type-checked override value in an explicit device specific settings patch.
/// </summary>
public sealed class DeviceSpecificSettingOverride
{
    public DeviceSpecificSettingKey Key { get; }

    public object Value { get; }

    internal DeviceSpecificSettingOverride(DeviceSpecificSettingKey key, object value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// Explicit device specific setting changes for one hierarchy level. Setting a key
/// cancels a pending removal of that key and removing it cancels a pending upsert.
/// </summary>
public sealed class DeviceSpecificSettingsChangeSet
{
    private readonly Dictionary<string, DeviceSpecificSettingOverride> upserts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeviceSpecificSettingKey> removals = new(StringComparer.Ordinal);

    public DeviceSpecificSettingsScope Scope { get; }

    public IReadOnlyCollection<DeviceSpecificSettingOverride> Upserts => upserts.Values;

    public IReadOnlyCollection<DeviceSpecificSettingKey> Removals => removals.Values;

    public bool IsEmpty => upserts.Count == 0 && removals.Count == 0;

    public DeviceSpecificSettingsChangeSet(DeviceSpecificSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
    }

    /// <summary>Stores the value as override of the key on this level.</summary>
    public DeviceSpecificSettingsChangeSet Set<TValue>(DeviceSpecificSettingKey<TValue> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ValidateScope(key);

        removals.Remove(key.DatabaseKey);
        upserts[key.DatabaseKey] = new DeviceSpecificSettingOverride(key, value);
        return this;
    }

    /// <summary>Removes the override of the key from this level, so that the level inherits the value again.</summary>
    public DeviceSpecificSettingsChangeSet Remove(DeviceSpecificSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ValidateScope(key);

        upserts.Remove(key.DatabaseKey);
        removals[key.DatabaseKey] = key;
        return this;
    }

    private void ValidateScope(DeviceSpecificSettingKey key)
    {
        if (!key.IsAllowedAt(Scope.ScopeType))
        {
            throw new ArgumentException(
                $"Setting '{key.DatabaseKey}' cannot be overridden at scope '{Scope.ScopeType}'.",
                nameof(key));
        }
    }
}
