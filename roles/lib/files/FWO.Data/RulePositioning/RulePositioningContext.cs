using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using System.Collections.ObjectModel;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Contains the caller-resolved inputs required to calculate one rule position.
/// </summary>
public sealed record RulePositioningContext
{
    /// <summary>Gets the implementation task receiving the calculated position.</summary>
    public WfImplTask ImplementationTask { get; init; }
    /// <summary>Gets the parent request task describing the new rule.</summary>
    public WfReqTask ParentRequestTask { get; init; }
    /// <summary>Gets the target gateway.</summary>
    public Device TargetGateway { get; init; }
    /// <summary>Gets the effective target management.</summary>
    public Management TargetManagement { get; init; }
    /// <summary>Gets the effective device-specific positioning settings.</summary>
    public GatewayDeviceSpecificSettings EffectiveSettings { get; init; }

    /// <summary>Gets the optional immutable ticket-task slice used by ticket-aware strategies.</summary>
    public IReadOnlyList<WfReqTask>? TicketRequestTasks { get; init; }

    /// <summary>Gets the current global settings snapshot for strategies that require it.</summary>
    public RulePositioningConfiguration? Configuration { get; init; }

    /// <summary>Creates one caller-resolved positioning context.</summary>
    public RulePositioningContext(
        WfImplTask implementationTask,
        WfReqTask parentRequestTask,
        Device targetGateway,
        Management targetManagement,
        GatewayDeviceSpecificSettings effectiveSettings,
        IReadOnlyCollection<WfReqTask>? ticketRequestTasks = null,
        RulePositioningConfiguration? configuration = null)
    {
        ImplementationTask = implementationTask;
        ParentRequestTask = parentRequestTask;
        TargetGateway = targetGateway;
        TargetManagement = targetManagement;
        EffectiveSettings = effectiveSettings;
        TicketRequestTasks = ticketRequestTasks == null
            ? null
            : new ReadOnlyCollection<WfReqTask>(ticketRequestTasks.ToList());
        Configuration = configuration;
    }
}
