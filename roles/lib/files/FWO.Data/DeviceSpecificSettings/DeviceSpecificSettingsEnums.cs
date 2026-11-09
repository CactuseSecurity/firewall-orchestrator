using System.Runtime.Serialization;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Level of the device specific settings hierarchy, from the most general (Global) to the most specific (Gateway).
/// </summary>
public enum DeviceSpecificScopeType
{
    Undefined,
    Global,
    DeviceType,
    Management,
    Gateway
}

/// <summary>
/// How implementation tasks are created for a level.
/// </summary>
public enum ProvisioningImplementationMode
{
    Undefined,
    Automatic,
    Manual,
    [EnumMember(Value = "reroute_to_external_ticket_system")]
    RerouteToExternalTicketSystem
}

/// <summary>
/// Logging behaviour of generated rules.
/// </summary>
public enum ProvisioningLoggingMode
{
    Undefined,
    [EnumMember(Value = "log")]
    Log,
    [EnumMember(Value = "log_track")]
    LogTrack,
    [EnumMember(Value = "none")]
    None
}

/// <summary>
/// Whether objects are created in the supermanager or in the submanager.
/// </summary>
public enum ProvisioningObjectCreationMode
{
    Undefined,
    Supermanager,
    Submanager,
    [EnumMember(Value = "ignore")]
    Ignore
}

/// <summary>
/// Which rule kinds (access, NAT, IPS) are handled for a level.
/// </summary>
public enum ProvisioningRuleType
{
    Undefined,
    [EnumMember(Value = "access")]
    AlwaysAccess,
    [EnumMember(Value = "accessAndNat")]
    HandleAccessAndNat,
    [EnumMember(Value = "accessAndIPS")]
    HandleAccessAndIps,
    [EnumMember(Value = "accessNatIPS")]
    HandleAccessNatIps,
    AppSecurity
}

/// <summary>
/// Where new rules are placed in the rulebase.
/// </summary>
public enum ProvisioningPositioningAlgorithm
{
    Undefined = 0,
    [EnumMember(Value = "cpInlineLayerPerZonePair")]
    CheckPointInlineLayerPerZonePair = 1,
    [EnumMember(Value = "endOfZone")]
    FortinetEndOfZone = 2,
    [EnumMember(Value = "endOfAppSection")]
    CheckPointEndOfAppSection = 3,
    [EnumMember(Value = "endOfAppSectionDistinguishCommonServices")]
    CheckPointEndOfAppSectionDistinguishCommonServices = 4,
    DefaultEndOfRulebase = 5,
    [EnumMember(Value = "allowCategoryEndOfAppSectionDistinguishCommonServices")]
    AllowCategoryEndOfAppSectionDistinguishCommonServices = 6,
    [EnumMember(Value = "cpLayerGuardEndOfZonePairSection")]
    CheckPointLayerGuardEndOfZonePairSection = 7
}

/// <summary>Gateways on which generated rules are installed.</summary>
public enum ProvisioningInstallOn
{
    Undefined,
    [EnumMember(Value = "ANY")]
    Any,
    [EnumMember(Value = "gatewaysInPath")]
    GatewaysInPath
}

/// <summary>Rule-specific implementation mode.</summary>
public enum ProvisioningRuleImplementationMode
{
    Undefined,
    Automatic,
    Manual,
    [EnumMember(Value = "rerouteToExternalTicketSystem")]
    RerouteToExternalTicketSystem
}

/// <summary>Where a provisioned object stores the ID of the modelling connection it implements.</summary>
public enum ProvisioningConnectionIdField
{
    Undefined,
    Name,
    Comment,
    CustomField
}
