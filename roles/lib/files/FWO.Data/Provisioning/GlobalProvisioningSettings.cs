namespace FWO.Data.Provisioning;

public class GlobalProvisioningSettings
{
    public ProvisioningSettingsScope Scope { get; set; } = new();

    public ProvisioningImplementationMode ImplementationMode { get; set; } = ProvisioningImplementationMode.FwoAuto;

    public string InstallOn { get; set; } = "ANY";

    public ProvisioningPathAnalysisAlgorithm PathAnalysisAlgorithm { get; set; } = ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet;

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

public class ManagementProvisioningSettings : DeviceTypeProvisioningSettings
{
    public ManagementProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.Management;
    }
}

public class GatewayProvisioningSettings : ManagementProvisioningSettings
{
    public GatewayProvisioningSettings()
    {
        Scope.ScopeType = ProvisioningScopeType.Gateway;
    }
}
