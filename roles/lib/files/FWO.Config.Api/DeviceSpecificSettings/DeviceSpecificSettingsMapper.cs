using FWO.Data.DeviceSpecificSettings;

namespace FWO.Config.Api.DeviceSpecificSettings;

/// <summary>
/// Reads and writes the DTO property that belongs to a device specific setting key.
/// </summary>
internal static class DeviceSpecificSettingsMapper
{
    /// <summary>Creates the settings DTO of a scope type holding only the compiled defaults.</summary>
    public static GlobalDeviceSpecificSettings CreateDefaults(DeviceSpecificScopeType scopeType)
    {
        return scopeType switch
        {
            DeviceSpecificScopeType.Global => new GlobalDeviceSpecificSettings(),
            DeviceSpecificScopeType.DeviceType => new DeviceTypeDeviceSpecificSettings(),
            DeviceSpecificScopeType.Management => new ManagementDeviceSpecificSettings(),
            DeviceSpecificScopeType.Gateway => new GatewayDeviceSpecificSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };
    }

    private static readonly Dictionary<DeviceSpecificSettingKey, System.Reflection.PropertyInfo> Properties =
        DeviceSpecificSettingKeys.All.Where(key => key.DtoGetter is null).ToDictionary(key => key, key =>
            typeof(GlobalDeviceSpecificSettings).GetProperty(char.ToUpperInvariant(key.DatabaseKey[0]) + key.DatabaseKey[1..])
                ?? throw new InvalidOperationException($"Device specific setting '{key.DatabaseKey}' has no DTO mapping."));

    /// <summary>Sets the typed DTO property of a canonical key.</summary>
    public static void SetValue(GlobalDeviceSpecificSettings settings, DeviceSpecificSettingKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(value);
        if (key.DtoSetter is not null)
        {
            key.DtoSetter(settings, value);
            return;
        }
        Properties[key].SetValue(settings, value);
    }

    /// <summary>Reads the typed DTO property of a canonical key.</summary>
    public static object GetValue(GlobalDeviceSpecificSettings settings, DeviceSpecificSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return key.DtoGetter is not null ? key.DtoGetter(settings) : Properties[key].GetValue(settings)!;
    }
}
