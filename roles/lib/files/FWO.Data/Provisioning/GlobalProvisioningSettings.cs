namespace FWO.Data.Provisioning;

/// <summary>Effective provisioning values, initialized with their compiled defaults.</summary>
public class GlobalProvisioningSettings
{
    public ProvisioningSettingsScope Scope { get; set; } = new();

    public ProvisioningImplementationMode ImplementationMode { get; set; } = ProvisioningImplementationMode.FwoAuto;

    public ProvisioningInstallOn InstallOn { get; set; } = ProvisioningInstallOn.Any;

    public ProvisioningPositioningAlgorithm PositioningAlgorithm { get; set; } = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;

    public ProvisioningLoggingMode RuleLoggingOverride { get; set; } = ProvisioningLoggingMode.Log;

    public ProvisioningObjectCreationMode ServiceObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode AddressObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode TimeObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode LabelCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningRuleType RuleType { get; set; } = ProvisioningRuleType.AlwaysAccess;

    public ProvisioningRuleCategory RuleCategory { get; set; } = ProvisioningRuleCategory.App;

    public string RuleZoneFromOverride { get; set; } = "ANY";

    public string RuleZoneToOverride { get; set; } = "ANY";

    public ProvisioningRuleImplementationMode ImplementationModeOverrideForRules { get; set; } = ProvisioningRuleImplementationMode.Automatic;

    public List<string> SecurityProfiles { get; set; } = new();

    public string OwnerIdCustomFieldName { get; set; } = "OwnerId";

    public string ChangeNumberCustomFieldName { get; set; } = "ChangeId";

    public ProvisioningPlanningMode PlanImplementation { get; set; } = ProvisioningPlanningMode.Enable;

    public ProvisioningPlanningMode PlanImplementationOverrideForRules { get; set; } = ProvisioningPlanningMode.Enable;

    public Dictionary<string, string> ModellingNamingConventions { get; set; } = ProvisioningModellingNamingConventions.CreateDefaults();

    public ProvisioningYesNo ModellingCreateAppZones { get; set; } = ProvisioningYesNo.Yes;

    public ProvisioningYesNo ModellingImplementNetworkAreaObjects { get; set; } = ProvisioningYesNo.No;

    public ProvisioningYesNo ModellingDynIpLabelsVisibleAsGroups { get; set; } = ProvisioningYesNo.Yes;

    public string Templates { get; set; } = "";

    /// <summary>Creates global settings.</summary>
    public GlobalProvisioningSettings() => Scope.ScopeType = ProvisioningScopeType.Global;
}

/// <summary>Effective settings for a device type, including inherited global values.</summary>
public class DeviceTypeProvisioningSettings : GlobalProvisioningSettings
{
    /// <summary>Creates device-type settings.</summary>
    public DeviceTypeProvisioningSettings() => Scope.ScopeType = ProvisioningScopeType.DeviceType;
}

/// <summary>Effective settings for a management.</summary>
public class ManagementProvisioningSettings : DeviceTypeProvisioningSettings
{
    /// <summary>Creates management settings.</summary>
    public ManagementProvisioningSettings() => Scope.ScopeType = ProvisioningScopeType.Management;
}

/// <summary>Effective settings for a gateway.</summary>
public class GatewayProvisioningSettings : ManagementProvisioningSettings
{
    /// <summary>Creates gateway settings.</summary>
    public GatewayProvisioningSettings() => Scope.ScopeType = ProvisioningScopeType.Gateway;
}
