using System.Collections.ObjectModel;

namespace FWO.Data.Provisioning;

/// <summary>Canonical typed provisioning keys and their permitted override levels.</summary>
public static class ProvisioningSettingKeys
{
    private static readonly List<ProvisioningScopeType> AllScopeTypes =
        [ProvisioningScopeType.Global, ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management, ProvisioningScopeType.Gateway];
    private static readonly List<ProvisioningScopeType> ManagerScopeTypes =
        [ProvisioningScopeType.Global, ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management];
    private static readonly List<ProvisioningScopeType> DeviceScopeTypes =
        [ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management, ProvisioningScopeType.Gateway];
    private static readonly List<ProvisioningScopeType> DeviceTypeScopeTypes = [ProvisioningScopeType.DeviceType];

    public static readonly ProvisioningSettingKey<ProvisioningInstallOn> InstallOn =
        new("installOn", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPositioningAlgorithm> PositioningAlgorithm =
        new("positioningAlgorithm", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningLoggingMode> RuleLoggingOverride =
        new("ruleLoggingOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> ServiceObjectCreation =
        new("serviceObjectCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> AddressObjectCreation =
        new("addressObjectCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> TimeObjectCreation =
        new("timeObjectCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> LabelCreation =
        new("labelCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleType> RuleType =
        new("ruleType", AllScopeTypes);

    public static readonly ProvisioningSettingKey<bool> CommonServiceSeparation =
        new("commonServiceSeparation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<string> RuleZoneFromOverride =
        new("ruleZoneFromOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<string> RuleZoneToOverride =
        new("ruleZoneToOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningImplementationMode> ImplementationMode =
        new("implementationMode", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleImplementationMode> ImplementationModeOverrideForRules =
        new("implementationModeOverrideForRules", AllScopeTypes);

    /// <summary>ID of the configured external ticket system; undefined (null) unless overridden.</summary>
    public static readonly ProvisioningSettingKey<int?> RerouteToExternalTicketSystem =
        new("rerouteToExternalTicketSystem", AllScopeTypes);

    public static readonly ProvisioningSettingKey<List<string>> SecurityProfiles =
        new("securityProfiles", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<string> OwnerIdCustomFieldName =
        new("ownerIdCustomFieldName", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> ChangeIdCustomFieldName =
        new("changeIdCustomFieldName", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> ImporterHostName =
        new("importerHostName", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<bool> PlanImplementation =
        new("planImplementation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<bool> PlanImplementationOverrideForRules =
        new("planImplementationOverrideForRules", AllScopeTypes);

    public static readonly ProvisioningSettingKey<bool> ModellingCreateAppZones =
        new("modellingCreateAppZones", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<bool> ModellingImplementNetworkAreaObjects =
        new("modellingImplementNetworkAreaObjects", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<bool> ModellingDynIpLabelsVisibleAsGroups =
        new("modellingDynIpLabelsVisibleAsGroups", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningConnectionIdField> ModellingConnectionIdField =
        new("modellingConnectionIdField", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> ModellingConnectionIdTemplate =
        new("modellingConnectionIdTemplate", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> Templates =
        new("templates", DeviceTypeScopeTypes);

    public static IReadOnlyList<ProvisioningSettingKey> All { get; } = new List<ProvisioningSettingKey>
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
        PlanImplementation,
        PlanImplementationOverrideForRules,
        ModellingCreateAppZones,
        ModellingImplementNetworkAreaObjects,
        ModellingDynIpLabelsVisibleAsGroups,
        ModellingConnectionIdField,
        ModellingConnectionIdTemplate,
        Templates,
    }.AsReadOnly();

    public static IReadOnlyDictionary<string, ProvisioningSettingKey> ByDatabaseKey { get; } =
        new ReadOnlyDictionary<string, ProvisioningSettingKey>(
            All.ToDictionary(key => key.DatabaseKey, StringComparer.Ordinal));
}
