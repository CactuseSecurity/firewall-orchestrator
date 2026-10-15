namespace FWO.Data.Provisioning;

/// <summary>Effective provisioning values, initialized with their compiled defaults.</summary>
public class GlobalProvisioningSettings
{
    public ProvisioningSettingsScope Scope { get; set; } = new();

    public ProvisioningInstallOn InstallOn { get; set; } = ProvisioningInstallOn.Any;

    public ProvisioningPositioningAlgorithm PositioningAlgorithm { get; set; } = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;

    public ProvisioningLoggingMode RuleLoggingOverride { get; set; } = ProvisioningLoggingMode.Log;

    public ProvisioningObjectCreationMode ServiceObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode AddressObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode TimeObjectCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningObjectCreationMode LabelCreation { get; set; } = ProvisioningObjectCreationMode.Supermanager;

    public ProvisioningRuleType RuleType { get; set; } = ProvisioningRuleType.AlwaysAccess;

    public bool CommonServiceSeparation { get; set; } = false;

    public string RuleZoneFromOverride { get; set; } = "ANY";

    public string RuleZoneToOverride { get; set; } = "ANY";

    public ProvisioningImplementationMode ImplementationMode { get; set; } = ProvisioningImplementationMode.Automatic;

    public ProvisioningRuleImplementationMode ImplementationModeOverrideForRules { get; set; } = ProvisioningRuleImplementationMode.Automatic;

    /// <summary>ID of the external ticket system to reroute to; null while undefined.</summary>
    public int? RerouteToExternalTicketSystem { get; set; }

    public List<string> SecurityProfiles { get; set; } = new();

    public string OwnerIdCustomFieldName { get; set; } = "OwnerId";

    public string ChangeIdCustomFieldName { get; set; } = "ChangeId";

    /// <summary>Host running the importer for provisioning; defaults to the FWO host name.</summary>
    public string ImporterHostName { get; set; } = System.Net.Dns.GetHostName();

    public bool PlanImplementation { get; set; } = true;

    public bool PlanImplementationOverrideForRules { get; set; } = true;

    public bool ModellingCreateAppZones { get; set; } = true;

    public bool ModellingImplementNetworkAreaObjects { get; set; } = false;

    public bool ModellingDynIpLabelsVisibleAsGroups { get; set; } = true;

    public ProvisioningConnectionIdField ModellingConnectionIdField { get; set; } = ProvisioningConnectionIdField.Name;

    public string ModellingConnectionIdTemplate { get; set; } = "FWOC{ID}";

    public string Templates { get; set; } = "{}";

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
