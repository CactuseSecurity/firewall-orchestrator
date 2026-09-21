using System.Collections.ObjectModel;

namespace FWO.Data.Provisioning;

/// <summary>
/// Canonical provisioning setting definitions used by the database and C# callers.
/// </summary>
public static class ProvisioningSettingKeys
{
    private static readonly ProvisioningScopeType[] AllScopeTypes =
    [
        ProvisioningScopeType.Global,
        ProvisioningScopeType.DeviceType,
        ProvisioningScopeType.Management,
        ProvisioningScopeType.Gateway
    ];

    private static readonly ProvisioningScopeType[] DeviceScopeTypes =
    [
        ProvisioningScopeType.DeviceType,
        ProvisioningScopeType.Management,
        ProvisioningScopeType.Gateway
    ];

    public static readonly ProvisioningSettingKey<ProvisioningImplementationMode> ImplementationMode =
        new("implementationMode", AllScopeTypes);

    public static readonly ProvisioningSettingKey<string> InstallOn =
        new("installOn", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPathAnalysisAlgorithm> PathAnalysisAlgorithm =
        new("pathAnalysisAlgorithm", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningLoggingMode> Logging =
        new("logging", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> ServiceObjectCreation =
        new("serviceObjectCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningObjectCreationMode> AddressObjectCreation =
        new("addressObjectCreation", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleType> RuleType =
        new("ruleType", AllScopeTypes);

    public static readonly ProvisioningSettingKey<string> Templates =
        new("templates", AllScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningPositioningAlgorithm> PositioningAlgorithm =
        new("positioningAlgorithm", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<ProvisioningRuleCategory> RuleCategory =
        new("ruleCategory", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<List<string>> SecurityProfiles =
        new("securityProfiles", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<string> ZoneFrom =
        new("zoneFrom", DeviceScopeTypes);

    public static readonly ProvisioningSettingKey<string> ZoneTo =
        new("zoneTo", DeviceScopeTypes);

    public static IReadOnlyList<ProvisioningSettingKey> All { get; } = Array.AsReadOnly<ProvisioningSettingKey>(
    [
        ImplementationMode,
        InstallOn,
        PathAnalysisAlgorithm,
        Logging,
        ServiceObjectCreation,
        AddressObjectCreation,
        RuleType,
        Templates,
        PositioningAlgorithm,
        RuleCategory,
        SecurityProfiles,
        ZoneFrom,
        ZoneTo
    ]);

    public static IReadOnlyDictionary<string, ProvisioningSettingKey> ByDatabaseKey { get; } =
        new ReadOnlyDictionary<string, ProvisioningSettingKey>(
            All.ToDictionary(key => key.DatabaseKey, StringComparer.Ordinal));
}
