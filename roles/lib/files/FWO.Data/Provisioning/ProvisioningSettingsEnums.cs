namespace FWO.Data.Provisioning;

/// <summary>
/// Level of the provisioning settings hierarchy, from the most general (Global) to the most specific (Gateway).
/// </summary>
public enum ProvisioningScopeType
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
    FwoAuto,
    Manual,
    TufinSc,
    None
}

/// <summary>
/// Logging behaviour of generated rules.
/// </summary>
public enum ProvisioningLoggingMode
{
    Undefined,
    Log,
    LogTrack,
    None
}

/// <summary>
/// Whether objects are created in the supermanager or in the submanager.
/// </summary>
public enum ProvisioningObjectCreationMode
{
    Undefined,
    Supermanager,
    Submanager
}

/// <summary>
/// Which rule kinds (access, NAT, IPS) are handled for a level.
/// </summary>
public enum ProvisioningRuleType
{
    Undefined,
    AlwaysAccess,
    HandleAccessAndNat,
    HandleAccessAndIps,
    HandleAccessNatIps
}

/// <summary>
/// Where new rules are placed in the rulebase.
/// </summary>
public enum ProvisioningPositioningAlgorithm
{
    Undefined,
    CheckPointInlineLayerPerZonePair,
    FortinetEndOfZone,
    CheckPointEndOfAppSection,
    CheckPointEndOfAppSectionDistinguishCommonServices,
    DefaultEndOfRulebase
}

/// <summary>
/// Whether a rule is treated as an application rule or as a common-service rule.
/// </summary>
public enum ProvisioningRuleCategory
{
    Undefined,
    App,
    CommonService
}
