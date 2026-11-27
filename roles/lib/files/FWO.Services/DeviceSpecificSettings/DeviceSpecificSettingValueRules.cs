using FWO.Data.DeviceSpecificSettings;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>
/// Value rules of device specific settings beyond their JSON type, shared by the settings page and the import: number
/// minimums, the "METHOD path" form of firewall write endpoints and the Check Point LayerGuard section name template.
/// </summary>
public static class DeviceSpecificSettingValueRules
{
    /// <summary>Smallest response timeout in seconds.</summary>
    public const int kMinRequestResponseTimeout = 1;

    /// <summary>Smallest number of attempts of an external request.</summary>
    public const int kMinRequestMaxAttempts = 1;

    /// <summary>Smallest number of job cycles between two attempts.</summary>
    public const int kMinRequestCyclesBetweenAttempts = 0;

    private static readonly Dictionary<DeviceSpecificSettingKey, int> NumberMinimums = new()
    {
        [DeviceSpecificSettingKeys.RequestResponseTimeout] = kMinRequestResponseTimeout,
        [DeviceSpecificSettingKeys.RequestMaxAttempts] = kMinRequestMaxAttempts,
        [DeviceSpecificSettingKeys.RequestCyclesBetweenAttempts] = kMinRequestCyclesBetweenAttempts
    };

    private static readonly HashSet<DeviceSpecificSettingKey> EndpointKeys = [.. FirewallWriteSettingKeys.Endpoints.Values];

    /// <summary>Smallest value of a number setting; 0 for number settings without a stricter minimum.</summary>
    public static int MinimumNumber(DeviceSpecificSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return NumberMinimums.GetValueOrDefault(key, 0);
    }

    /// <summary>
    /// Checks a typed value against the rules of its key and returns the value to store: a firewall write endpoint in
    /// its canonical form, every other value unchanged. An empty endpoint stands for the shipped default and is kept.
    /// </summary>
    /// <exception cref="ArgumentException">The value breaks a rule of its key.</exception>
    public static object Normalize(DeviceSpecificSettingKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (value is int number && number < MinimumNumber(key))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Setting '{key.DatabaseKey}' requires a whole number of at least {MinimumNumber(key)}.");
        }

        if (value is string endpoint && EndpointKeys.Contains(key) && !string.IsNullOrWhiteSpace(endpoint))
        {
            return FirewallWriteEndpoint.Parse(endpoint).ToString();
        }

        if (key == DeviceSpecificSettingKeys.CheckPointLayerGuardSectionNameTemplate
            && !CheckPointSectionNameTemplate.Parse((string)value).IsSuccess)
        {
            // The settings page recognizes this error by the key as parameter name.
            throw new ArgumentException("The Check Point LayerGuard section name template is invalid.", key.DatabaseKey);
        }

        return value;
    }
}
