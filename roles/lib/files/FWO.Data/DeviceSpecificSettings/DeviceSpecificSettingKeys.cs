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

    public static readonly DeviceSpecificSettingKey<string> CheckPointLayerGuardSectionNameTemplate =
        new("checkPointLayerGuardSectionNameTemplate", AllScopeTypes);

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

    public static readonly DeviceSpecificSettingKey<ProvisioningRuleImplementationMode> ImplementationModeForRules =
        new("implementationModeForRules", AllScopeTypes);

    /// <summary>External ticket system tasks are handed to in the external ticket system mode; undefined unless overridden.</summary>
    public static readonly DeviceSpecificSettingKey<ExternalTicketSystemKind> RerouteToExternalTicketSystem =
        new("rerouteToExternalTicketSystem", AllScopeTypes);

    /// <summary>Base URL of the Tufin SecureChange API.</summary>
    public static readonly DeviceSpecificSettingKey<string> ScUrl =
        new("scUrl", ManagerScopeTypes);

    /// <summary>ID of the credential used to log into Tufin SecureChange; undefined (null) unless overridden.</summary>
    public static readonly DeviceSpecificSettingKey<int?> ScCredential =
        new("scCredential", ManagerScopeTypes);

    /// <summary>Whether the Tufin SecureChange user ID of the requester is looked up and set as requester of the ticket.</summary>
    public static readonly DeviceSpecificSettingKey<bool> ScLookupRequesterId =
        new("scLookupRequesterId", ManagerScopeTypes);

    /// <summary>Seconds a call to the external ticket system or the firewall may take before it fails.</summary>
    public static readonly DeviceSpecificSettingKey<int> RequestResponseTimeout =
        new("requestResponseTimeout", ManagerScopeTypes);

    /// <summary>How often an external request is sent before it is rejected, if its errors may pass.</summary>
    public static readonly DeviceSpecificSettingKey<int> RequestMaxAttempts =
        new("requestMaxAttempts", ManagerScopeTypes);

    /// <summary>Job cycles to wait before the next attempt, multiplied by the number of attempts so far.</summary>
    public static readonly DeviceSpecificSettingKey<int> RequestCyclesBetweenAttempts =
        new("requestCyclesBetweenAttempts", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<List<string>> SecurityProfiles =
        new("securityProfiles", DeviceScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> OwnerIdCustomFieldName =
        new("ownerIdCustomFieldName", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> ChangeIdCustomFieldName =
        new("changeIdCustomFieldName", ManagerScopeTypes);

    public static readonly DeviceSpecificSettingKey<string> ImporterHostName =
        new("importerHostName", ManagerScopeTypes);

    /// <summary>Whether the importer offers the SHA-1 SSH key exchange diffie-hellman-group14-sha1 (Cisco ASA).</summary>
    public static readonly DeviceSpecificSettingKey<bool> SshLegacyKeyExchange =
        new("sshLegacyKeyExchange", ManagerScopeTypes);

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

    /// <summary>
    /// All canonical keys, including the firewall write keys of <see cref="FirewallWriteSettingKeys"/> and the SecureChange
    /// template keys of <see cref="SecureChangeTemplateSettingKeys"/>.
    /// </summary>
    public static IReadOnlyList<DeviceSpecificSettingKey> All { get; } = new List<DeviceSpecificSettingKey>
    {
        InstallOn,
        PositioningAlgorithm,
        CheckPointLayerGuardSectionNameTemplate,
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
        ImplementationModeForRules,
        RerouteToExternalTicketSystem,
        ScUrl,
        ScCredential,
        ScLookupRequesterId,
        RequestResponseTimeout,
        RequestMaxAttempts,
        RequestCyclesBetweenAttempts,
        SecurityProfiles,
        OwnerIdCustomFieldName,
        ChangeIdCustomFieldName,
        ImporterHostName,
        SshLegacyKeyExchange,
        AutoPlanImplementation,
        AutoPlanImplementationForRules,
        ModellingCreateAppZones,
        ModellingImplementNetworkAreaObjects,
        ModellingDynIpLabelsVisibleAsGroups,
        ModellingConnectionIdField,
        ModellingConnectionIdTemplate,
        Templates,
    }.Concat(FirewallWriteSettingKeys.All).Concat(SecureChangeTemplateSettingKeys.All).ToList().AsReadOnly();

    public static IReadOnlyDictionary<string, DeviceSpecificSettingKey> ByDatabaseKey { get; } =
        new ReadOnlyDictionary<string, DeviceSpecificSettingKey>(
            All.ToDictionary(key => key.DatabaseKey, StringComparer.Ordinal));
}
