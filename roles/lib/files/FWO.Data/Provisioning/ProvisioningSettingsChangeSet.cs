namespace FWO.Data.Provisioning;

/// <summary>
/// A type-checked override value in an explicit provisioning settings patch.
/// </summary>
public sealed class ProvisioningSettingOverride
{
    public ProvisioningSettingKey Key { get; }

    public object Value { get; }

    internal ProvisioningSettingOverride(ProvisioningSettingKey key, object value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// Explicit provisioning setting changes for one hierarchy level. Setting a key
/// cancels a pending removal of that key and removing it cancels a pending upsert.
/// </summary>
public sealed class ProvisioningSettingsChangeSet
{
    private readonly Dictionary<string, ProvisioningSettingOverride> upserts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProvisioningSettingKey> removals = new(StringComparer.Ordinal);

    public ProvisioningSettingsScope Scope { get; }

    public IReadOnlyCollection<ProvisioningSettingOverride> Upserts => upserts.Values;

    public IReadOnlyCollection<ProvisioningSettingKey> Removals => removals.Values;

    public bool IsEmpty => upserts.Count == 0 && removals.Count == 0;

    public ProvisioningSettingsChangeSet(ProvisioningSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
    }

    public ProvisioningSettingsChangeSet Set<TValue>(ProvisioningSettingKey<TValue> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ValidateScope(key);

        removals.Remove(key.DatabaseKey);
        upserts[key.DatabaseKey] = new ProvisioningSettingOverride(key, value);
        return this;
    }

    public ProvisioningSettingsChangeSet Remove(ProvisioningSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ValidateScope(key);

        upserts.Remove(key.DatabaseKey);
        removals[key.DatabaseKey] = key;
        return this;
    }

    private void ValidateScope(ProvisioningSettingKey key)
    {
        if (!key.IsAllowedAt(Scope.ScopeType))
        {
            throw new ArgumentException(
                $"Setting '{key.DatabaseKey}' cannot be overridden at scope '{Scope.ScopeType}'.",
                nameof(key));
        }
    }
}
