using FWO.Data.Provisioning;

namespace FWO.Config.Api.Provisioning;

/// <summary>
/// Reads and writes the DTO property that belongs to a provisioning setting key.
/// </summary>
internal static class ProvisioningSettingsMapper
{
    /// <summary>Creates the settings DTO of a scope type holding only the compiled defaults.</summary>
    public static GlobalProvisioningSettings CreateDefaults(ProvisioningScopeType scopeType)
    {
        return scopeType switch
        {
            ProvisioningScopeType.Global => new GlobalProvisioningSettings(),
            ProvisioningScopeType.DeviceType => new DeviceTypeProvisioningSettings(),
            ProvisioningScopeType.Management => new ManagementProvisioningSettings(),
            ProvisioningScopeType.Gateway => new GatewayProvisioningSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };
    }

    private static readonly IReadOnlyDictionary<ProvisioningSettingKey, System.Reflection.PropertyInfo> Properties =
        ProvisioningSettingKeys.All.ToDictionary(key => key, key =>
            typeof(GlobalProvisioningSettings).GetProperty(char.ToUpperInvariant(key.DatabaseKey[0]) + key.DatabaseKey[1..])
                ?? throw new InvalidOperationException($"Provisioning setting '{key.DatabaseKey}' has no DTO mapping."));

    /// <summary>Sets the typed DTO property of a canonical key.</summary>
    public static void SetValue(GlobalProvisioningSettings settings, ProvisioningSettingKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(value);
        Properties[key].SetValue(settings, value);
    }

    /// <summary>Reads the typed DTO property of a canonical key.</summary>
    public static object GetValue(GlobalProvisioningSettings settings, ProvisioningSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Properties[key].GetValue(settings)!;
    }
}
