namespace FWO.Data.DeviceSpecificSettings;

/// <summary>Effective device specific setting values, initialized with their compiled defaults.</summary>
public class GlobalDeviceSpecificSettings
{
    public DeviceSpecificSettingsScope Scope { get; set; } = new();

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

    /// <summary>
    /// Host running the importer for the import. Empty by default: the importer running the import uses
    /// its own host name, so the default does not depend on which FWO host resolves the settings.
    /// </summary>
    public string ImporterHostName { get; set; } = "";

    /// <summary>
    /// Whether the implementation tasks of a request task are created automatically once the request task is
    /// approved. When false, a planner creates them. Applies to every task type; rule tasks additionally need
    /// <see cref="AutoPlanImplementationForRules"/>.
    /// </summary>
    public bool AutoPlanImplementation { get; set; } = true;

    /// <summary>
    /// Whether the implementation tasks of rule tasks (access, rule_modify, rule_delete) are created automatically.
    /// Only effective together with <see cref="AutoPlanImplementation"/>; when false, a planner creates them.
    /// </summary>
    public bool AutoPlanImplementationForRules { get; set; } = false;

    /// <summary>
    /// Returns whether the implementation tasks of a kind of task are created automatically: rule tasks need both auto
    /// plan settings, every other task type <see cref="AutoPlanImplementation"/>.
    /// </summary>
    /// <param name="isRuleTask">True for access, rule_modify and rule_delete tasks.</param>
    public bool IsAutoPlanned(bool isRuleTask)
    {
        return AutoPlanImplementation && (!isRuleTask || AutoPlanImplementationForRules);
    }

    public bool ModellingCreateAppZones { get; set; } = true;

    public bool ModellingImplementNetworkAreaObjects { get; set; } = false;

    public bool ModellingDynIpLabelsVisibleAsGroups { get; set; } = true;

    public ProvisioningConnectionIdField ModellingConnectionIdField { get; set; } = ProvisioningConnectionIdField.Name;

    public string ModellingConnectionIdTemplate { get; set; } = "FWOC{ID}";

    public string Templates { get; set; } = "{}";

    /// <summary>Whether the server certificate of the firewall management is checked when writing to the firewall.</summary>
    public bool FwWriteCheckCertificates { get; set; } = true;

    /// <summary>Whether the policy is installed on the gateway after a firewall write.</summary>
    public bool FwWriteInstallPolicy { get; set; } = false;

    /// <summary>Whether objects, services and groups are deleted on the firewall; rules are always deleted when a task says so.</summary>
    public bool FwWriteDeleteObjects { get; set; } = false;

    /// <summary>Endpoint overrides ("METHOD path") per firewall write operation; a missing or empty entry uses the shipped default.</summary>
    public Dictionary<FirewallWriteOperation, string> FwWriteEndpoints { get; set; } = [];

    /// <summary>Body template overrides per firewall write operation; a missing or empty entry uses the shipped default.</summary>
    public Dictionary<FirewallWriteOperation, string> FwWriteTemplates { get; set; } = [];

    /// <summary>
    /// Tufin SecureChange ticket template overrides per task type; a missing task type or an empty part uses the shipped default.
    /// </summary>
    public Dictionary<SCTaskType, ExternalTicketTemplate> SecureChangeTemplates { get; set; } = [];

    /// <summary>Creates global settings.</summary>
    public GlobalDeviceSpecificSettings() => Scope.ScopeType = DeviceSpecificScopeType.Global;
}

/// <summary>Effective settings for a device type, including inherited global values.</summary>
public class DeviceTypeDeviceSpecificSettings : GlobalDeviceSpecificSettings
{
    /// <summary>Creates device-type settings.</summary>
    public DeviceTypeDeviceSpecificSettings() => Scope.ScopeType = DeviceSpecificScopeType.DeviceType;
}

/// <summary>Effective settings for a management.</summary>
public class ManagementDeviceSpecificSettings : DeviceTypeDeviceSpecificSettings
{
    /// <summary>Creates management settings.</summary>
    public ManagementDeviceSpecificSettings() => Scope.ScopeType = DeviceSpecificScopeType.Management;
}

/// <summary>Effective settings for a gateway.</summary>
public class GatewayDeviceSpecificSettings : ManagementDeviceSpecificSettings
{
    /// <summary>Creates gateway settings.</summary>
    public GatewayDeviceSpecificSettings() => Scope.ScopeType = DeviceSpecificScopeType.Gateway;
}
