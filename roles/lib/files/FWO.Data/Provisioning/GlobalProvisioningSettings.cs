namespace FWO.Data.Provisioning;

/// <summary>
/// Provisioning settings that can be set from the global level downwards, initialized with their compiled defaults.
/// </summary>
public class GlobalProvisioningSettings
{
    public ProvisioningSettingsScope Scope { get; set; } = new();

    public ProvisioningImplementationMode ImplementationMode { get; set; } = ProvisioningImplementationMode.FwoAuto;

    public string InstallOn { get; set; } = "ANY";

    public ProvisioningLoggingMode Logging { get; set; } = ProvisioningLoggingMode.Log;

    public ProvisioningObjectCreationMode ServiceObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode AddressObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningRuleType RuleType { get; set; } = ProvisioningRuleType.AlwaysAccess;

    public string Templates { get; set; } = "";

    public GlobalProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.Global;
    }
}

/// <summary>
/// Provisioning settings of a device type; adds the device-level settings that are not available globally.
/// </summary>
public class DeviceTypeProvisioningSettings : GlobalProvisioningSettings
{
    public ProvisioningPositioningAlgorithm PositioningAlgorithm { get; set; } = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;

    public ProvisioningRuleCategory RuleCategory { get; set; } = ProvisioningRuleCategory.App;

    public List<string> SecurityProfiles { get; set; } = new List<string>();

    public string ZoneFrom { get; set; } = "ANY";

    public string ZoneTo { get; set; } = "ANY";

    public DeviceTypeProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.DeviceType;
    }
}

/// <summary>Provisioning settings of a management.</summary>
public class ManagementProvisioningSettings : DeviceTypeProvisioningSettings
{
    public ManagementProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.Management;
    }
}

/// <summary>Provisioning settings of a gateway.</summary>
public class GatewayProvisioningSettings : ManagementProvisioningSettings
{
    public GatewayProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.Gateway;
    }
}
