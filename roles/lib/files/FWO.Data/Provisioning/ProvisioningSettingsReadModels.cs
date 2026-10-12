using System.Collections.ObjectModel;

namespace FWO.Data.Provisioning;

/// <summary>
/// Describes where an effective provisioning value originated. A null scope means
/// that no override exists in the hierarchy and the compiled DTO default is used.
/// </summary>
public sealed record ProvisioningSettingValueSource(ProvisioningSettingsScope? Scope)
{
    public bool UsesCompiledDefault => Scope is null;
}

/// <summary>
/// One effective provisioning value together with the node that supplied it.
/// </summary>
public sealed record ResolvedProvisioningValue<TValue>(
    ProvisioningSettingKey<TValue> Key,
    TValue Value,
    ProvisioningSettingValueSource Source);

/// <summary>
/// Effective settings for one hierarchy level plus the persistence information
/// needed to distinguish local overrides from inherited values.
/// </summary>
public sealed class ProvisioningSettingsLevel<TSettings>
    where TSettings : GlobalProvisioningSettings
{
    public ProvisioningSettingsScope Scope { get; }

    public TSettings Settings { get; }

    public IReadOnlySet<ProvisioningSettingKey> DirectOverrides { get; }

    public IReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource> ValueSources { get; }

    public ProvisioningSettingsLevel(
        ProvisioningSettingsScope scope,
        TSettings settings,
        IEnumerable<ProvisioningSettingKey>? directOverrides = null,
        IEnumerable<KeyValuePair<ProvisioningSettingKey, ProvisioningSettingValueSource>>? valueSources = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        Scope = scope;
        Settings = settings;
        DirectOverrides = (directOverrides ?? []).ToHashSet();
        ValueSources = new ReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource>(
            (valueSources ?? []).ToDictionary(entry => entry.Key, entry => entry.Value));
    }
}
