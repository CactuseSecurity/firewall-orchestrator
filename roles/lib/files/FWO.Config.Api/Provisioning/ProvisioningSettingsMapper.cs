using FWO.Data.Provisioning;

namespace FWO.Config.Api.Provisioning;

internal static class ProvisioningSettingsMapper
{
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

    public static void SetValue(
        GlobalProvisioningSettings settings,
        ProvisioningSettingKey key,
        object value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        switch (key.DatabaseKey)
        {
            case "implementationMode":
                settings.ImplementationMode = (ProvisioningImplementationMode)value;
                break;
            case "installOn":
                settings.InstallOn = (string)value;
                break;
            case "pathAnalysisAlgorithm":
                settings.PathAnalysisAlgorithm = (ProvisioningPathAnalysisAlgorithm)value;
                break;
            case "logging":
                settings.Logging = (ProvisioningLoggingMode)value;
                break;
            case "serviceObjectCreation":
                settings.ServiceObjectCreation = (ProvisioningObjectCreationMode)value;
                break;
            case "addressObjectCreation":
                settings.AddressObjectCreation = (ProvisioningObjectCreationMode)value;
                break;
            case "ruleType":
                settings.RuleType = (ProvisioningRuleType)value;
                break;
            case "templates":
                settings.Templates = (string)value;
                break;
            case "positioningAlgorithm":
                RequireDeviceSettings(settings, key).PositioningAlgorithm = (ProvisioningPositioningAlgorithm)value;
                break;
            case "ruleCategory":
                RequireDeviceSettings(settings, key).RuleCategory = (ProvisioningRuleCategory)value;
                break;
            case "securityProfiles":
                RequireDeviceSettings(settings, key).SecurityProfiles = (List<string>)value;
                break;
            case "zoneFrom":
                RequireDeviceSettings(settings, key).ZoneFrom = (string)value;
                break;
            case "zoneTo":
                RequireDeviceSettings(settings, key).ZoneTo = (string)value;
                break;
            default:
                throw new InvalidOperationException($"Provisioning setting '{key.DatabaseKey}' has no DTO mapping.");
        }
    }

    public static object GetValue(GlobalProvisioningSettings settings, ProvisioningSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(key);

        return key.DatabaseKey switch
        {
            "implementationMode" => settings.ImplementationMode,
            "installOn" => settings.InstallOn,
            "pathAnalysisAlgorithm" => settings.PathAnalysisAlgorithm,
            "logging" => settings.Logging,
            "serviceObjectCreation" => settings.ServiceObjectCreation,
            "addressObjectCreation" => settings.AddressObjectCreation,
            "ruleType" => settings.RuleType,
            "templates" => settings.Templates,
            "positioningAlgorithm" => RequireDeviceSettings(settings, key).PositioningAlgorithm,
            "ruleCategory" => RequireDeviceSettings(settings, key).RuleCategory,
            "securityProfiles" => RequireDeviceSettings(settings, key).SecurityProfiles,
            "zoneFrom" => RequireDeviceSettings(settings, key).ZoneFrom,
            "zoneTo" => RequireDeviceSettings(settings, key).ZoneTo,
            _ => throw new InvalidOperationException($"Provisioning setting '{key.DatabaseKey}' has no DTO mapping.")
        };
    }

    private static DeviceTypeProvisioningSettings RequireDeviceSettings(
        GlobalProvisioningSettings settings,
        ProvisioningSettingKey key)
    {
        return settings as DeviceTypeProvisioningSettings
            ?? throw new InvalidOperationException(
                $"Provisioning setting '{key.DatabaseKey}' cannot be applied to '{settings.GetType().Name}'.");
    }
}
