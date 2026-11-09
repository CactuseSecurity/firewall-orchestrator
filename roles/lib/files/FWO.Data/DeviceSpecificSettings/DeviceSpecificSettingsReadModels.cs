using System.Collections.ObjectModel;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Describes where an effective device specific setting value originated. A null scope means
/// that no override exists in the hierarchy and the compiled DTO default is used.
/// </summary>
public sealed record DeviceSpecificSettingValueSource(DeviceSpecificSettingsScope? Scope)
{
    public bool UsesCompiledDefault => Scope is null;
}

/// <summary>
/// One effective device specific setting value together with the node that supplied it.
/// </summary>
public sealed record ResolvedDeviceSpecificValue<TValue>(
    DeviceSpecificSettingKey<TValue> Key,
    TValue Value,
    DeviceSpecificSettingValueSource Source);

/// <summary>
/// Effective settings for one hierarchy level plus the persistence information
/// needed to distinguish local overrides from inherited values.
/// </summary>
public sealed class DeviceSpecificSettingsLevel<TSettings>
    where TSettings : GlobalDeviceSpecificSettings
{
    public DeviceSpecificSettingsScope Scope { get; }

    public TSettings Settings { get; }

    public IReadOnlySet<DeviceSpecificSettingKey> DirectOverrides { get; }

    public IReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource> ValueSources { get; }

    public DeviceSpecificSettingsLevel(
        DeviceSpecificSettingsScope scope,
        TSettings settings,
        IEnumerable<DeviceSpecificSettingKey>? directOverrides = null,
        IEnumerable<KeyValuePair<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource>>? valueSources = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settings);

        Scope = scope;
        Settings = settings;
        DirectOverrides = (directOverrides ?? []).ToHashSet();
        ValueSources = new ReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource>(
            (valueSources ?? []).ToDictionary(entry => entry.Key, entry => entry.Value));
    }
}
