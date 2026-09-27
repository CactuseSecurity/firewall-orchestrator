namespace FWO.Data.Provisioning;

public enum ProvisioningScopeType
{
    Undefined,
    Global,
    DeviceType,
    Management,
    Gateway
}

public enum ProvisioningImplementationMode
{
    Undefined,
    FwoAuto,
    Manual,
    TufinSc,
    None
}

public enum ProvisioningPathAnalysisAlgorithm
{
    Undefined,
    StaticListsPerSubnet,
    ManualPlanning,
    AskExternalApi
}

public enum ProvisioningLoggingMode
{
    Undefined,
    Log,
    LogTrack,
    None
}

public enum ProvisioningObjectCreationMode
{
    Undefined,
    Supermanager,
    Submanager
}

public enum ProvisioningRuleType
{
    Undefined,
    AlwaysAccess,
    HandleAccessAndNat,
    HandleAccessAndIps,
    HandleAccessNatIps
}

public enum ProvisioningPositioningAlgorithm
{
    Undefined,
    CheckPointInlineLayerPerZonePair,
    FortinetEndOfZone,
    CheckPointEndOfAppSection,
    CheckPointEndOfAppSectionDistinguishCommonServices,
    DefaultEndOfRulebase
}

public enum ProvisioningRuleCategory
{
    Undefined,
    App,
    CommonService
}
