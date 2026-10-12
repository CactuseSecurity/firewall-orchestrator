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
    private static readonly List<ProvisioningScopeType> TemplateScopeTypes =
        [ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management];
    private static readonly List<ProvisioningScopeType> GlobalScopeTypes = [ProvisioningScopeType.Global];

    public static readonly ProvisioningSettingKey<ProvisioningImplementationMode> ImplementationMode =
        new("implementationMode", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningInstallOn> InstallOn =
        new("installOn", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPositioningAlgorithm> PositioningAlgorithm =
        new("positioningAlgorithm", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningLoggingMode> RuleLoggingOverride =
        new("ruleLoggingOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> ServiceObjectCreation =
        new("serviceObjectCreation", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> AddressObjectCreation =
        new("addressObjectCreation", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> TimeObjectCreation =
        new("timeObjectCreation", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> LabelCreation =
        new("labelCreation", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleType> RuleType =
        new("ruleType", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleCategory> RuleCategory =
        new("ruleCategory", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> RuleZoneFromOverride =
        new("ruleZoneFromOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<string> RuleZoneToOverride =
        new("ruleZoneToOverride", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleImplementationMode> ImplementationModeOverrideForRules =
        new("implementationModeOverrideForRules", AllScopeTypes);

    public static readonly ProvisioningSettingKey<List<string>> SecurityProfiles =
        new("securityProfiles", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<string> OwnerIdCustomFieldName =
        new("ownerIdCustomFieldName", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> ChangeNumberCustomFieldName =
        new("changeNumberCustomFieldName", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPlanningMode> PlanImplementation =
        new("planImplementation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPlanningMode> PlanImplementationOverrideForRules =
        new("planImplementationOverrideForRules", AllScopeTypes);

    public static readonly ProvisioningSettingKey<Dictionary<string, string>> ModellingNamingConventions =
        new("modellingNamingConventions", GlobalScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningYesNo> ModellingCreateAppZones =
        new("modellingCreateAppZones", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningYesNo> ModellingImplementNetworkAreaObjects =
        new("modellingImplementNetworkAreaObjects", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningYesNo> ModellingDynIpLabelsVisibleAsGroups =
        new("modellingDynIpLabelsVisibleAsGroups", ManagerScopeTypes);

    public static readonly ProvisioningSettingKey<string> Templates =
        new("templates", TemplateScopeTypes);

    public static IReadOnlyList<ProvisioningSettingKey> All { get; } = new List<ProvisioningSettingKey>
    {
        ImplementationMode,
        InstallOn,
        PositioningAlgorithm,
        RuleLoggingOverride,
        ServiceObjectCreation,
        AddressObjectCreation,
        TimeObjectCreation,
        LabelCreation,
        RuleType,
        RuleCategory,
        RuleZoneFromOverride,
        RuleZoneToOverride,
        ImplementationModeOverrideForRules,
        SecurityProfiles,
        OwnerIdCustomFieldName,
        ChangeNumberCustomFieldName,
        PlanImplementation,
        PlanImplementationOverrideForRules,
        ModellingNamingConventions,
        ModellingCreateAppZones,
        ModellingImplementNetworkAreaObjects,
        ModellingDynIpLabelsVisibleAsGroups,
        Templates,
    }.AsReadOnly();

    public static IReadOnlyDictionary<string, ProvisioningSettingKey> ByDatabaseKey { get; } =
        new ReadOnlyDictionary<string, ProvisioningSettingKey>(
            All.ToDictionary(key => key.DatabaseKey, StringComparer.Ordinal));
}
