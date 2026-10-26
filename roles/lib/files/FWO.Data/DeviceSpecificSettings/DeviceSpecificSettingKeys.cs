using System.Collections.ObjectModel;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>Canonical typed device specific setting keys and their permitted override levels.</summary>
public static class DeviceSpecificSettingKeys
{
    private static readonly List<DeviceSpecificScopeType> AllScopeTypes =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopeType> ManagerScopeTypes =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management];
    private static readonly List<DeviceSpecificScopeType> DeviceScopeTypes =
        [DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopeType> DeviceTypeScopeTypes = [DeviceSpecificScopeType.DeviceType];

    public static readonly DeviceSpecificSettingKey<ProvisioningInstallOn> InstallOn =
        new("installOn", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningPositioningAlgorithm> PositioningAlgorithm =
        new("positioningAlgorithm", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningLoggingMode> RuleLoggingOverride =
        new("ruleLoggingOverride", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningObjectCreationMode> ServiceObjectCreation =
        new("serviceObjectCreation", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningObjectCreationMode> AddressObjectCreation =
        new("addressObjectCreation", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningObjectCreationMode> TimeObjectCreation =
        new("timeObjectCreation", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningObjectCreationMode> LabelCreation =
        new("labelCreation", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningRuleType> RuleType =
        new("ruleType", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<bool> CommonServiceSeparation =
        new("commonServiceSeparation", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> RuleZoneFromOverride =
        new("ruleZoneFromOverride", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> RuleZoneToOverride =
        new("ruleZoneToOverride", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningImplementationMode> ImplementationMode =
        new("implementationMode", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningRuleImplementationMode> ImplementationModeOverrideForRules =
        new("implementationModeOverrideForRules", AllScopeTypes);

    /// <summary>ID of the configured external ticket system; undefined (null) unless overridden.</summary>
    public static readonly DeviceSpecificSettingKey<int?> RerouteToExternalTicketSystem =
        new("rerouteToExternalTicketSystem", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<List<string>> SecurityProfiles =
        new("securityProfiles", DeviceScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> OwnerIdCustomFieldName =
        new("ownerIdCustomFieldName", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> ChangeIdCustomFieldName =
        new("changeIdCustomFieldName", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> ImporterHostName =
        new("importerHostName", ManagerScopeTypes);

    /// <summary>Automatic creation of implementation tasks for every task type.</summary>
    public static readonly DeviceSpecificSettingKey<bool> AutoPlanImplementation =
        new("autoPlanImplementation", AllScopeTypes);

    /// <summary>Automatic creation of implementation tasks for rule tasks, only together with AutoPlanImplementation.</summary>
    public static readonly DeviceSpecificSettingKey<bool> AutoPlanImplementationForRules =
        new("autoPlanImplementationForRules", AllScopeTypes);

    public static readonly DeviceSpecificSettingKey<bool> ModellingCreateAppZones =
        new("modellingCreateAppZones", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<bool> ModellingImplementNetworkAreaObjects =
        new("modellingImplementNetworkAreaObjects", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<bool> ModellingDynIpLabelsVisibleAsGroups =
        new("modellingDynIpLabelsVisibleAsGroups", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<ProvisioningConnectionIdField> ModellingConnectionIdField =
        new("modellingConnectionIdField", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> ModellingConnectionIdTemplate =
        new("modellingConnectionIdTemplate", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> Templates =
        new("templates", DeviceTypeScopeTypes);

    public static IReadOnlyList<DeviceSpecificSettingKey> All { get; } = new List<DeviceSpecificSettingKey>
    {
        InstallOn,
        PositioningAlgorithm,
        RuleLoggingOverride,
        ServiceObjectCreation,
        AddressObjectCreation,
        TimeObjectCreation,
        LabelCreation,
        RuleType,
        CommonServiceSeparation,
        RuleZoneFromOverride,
        RuleZoneToOverride,
        ImplementationMode,
        ImplementationModeOverrideForRules,
        RerouteToExternalTicketSystem,
        SecurityProfiles,
        OwnerIdCustomFieldName,
        ChangeIdCustomFieldName,
        ImporterHostName,
        AutoPlanImplementation,
        AutoPlanImplementationForRules,
        ModellingCreateAppZones,
        ModellingImplementNetworkAreaObjects,
        ModellingDynIpLabelsVisibleAsGroups,
        ModellingConnectionIdField,
        ModellingConnectionIdTemplate,
        Templates,
    }.AsReadOnly();

    public static IReadOnlyDictionary<string, DeviceSpecificSettingKey> ByDatabaseKey { get; } =
        new ReadOnlyDictionary<string, DeviceSpecificSettingKey>(
            All.ToDictionary(key => key.DatabaseKey, StringComparer.Ordinal));
}
