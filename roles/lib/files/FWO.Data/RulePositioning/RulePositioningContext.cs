using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Contains the caller-resolved inputs required to calculate one rule position.
/// </summary>
public sealed record RulePositioningContext(
    WfImplTask ImplementationTask,
    WfReqTask ParentRequestTask,
    Device TargetGateway,
    Management TargetManagement,
    GatewayDeviceSpecificSettings EffectiveSettings);
