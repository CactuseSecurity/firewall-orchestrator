using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule address ranges from workflow state.</summary>
public interface ICreationRuleNetworkInputResolver
{
    /// <summary>Resolves the effective source and destination address ranges at the access task.</summary>
    Task<PositioningResult<CreationRuleNetworkInput, CreationRuleNetworkResolutionFailureCategory>> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken);
}

/// <summary>Safe diagnostics for creation-rule network-resolution failures.</summary>
public static class CreationRuleNetworkResolutionFailureDiagnostics
{
    /// <summary>Returns the fixed safe diagnostic for a network-resolution failure.</summary>
    public static string GetDiagnostic(this CreationRuleNetworkResolutionFailureCategory category) => category switch
    {
        CreationRuleNetworkResolutionFailureCategory.InvalidInput => "The creation-rule network input is invalid.",
        CreationRuleNetworkResolutionFailureCategory.UnsupportedTask => "The request task is not a supported access-rule creation.",
        CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid => "A referenced network change is ordered after the access rule.",
        CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved => "A referenced network object could not be resolved.",
        CreationRuleNetworkResolutionFailureCategory.GroupUnresolved => "A referenced network group could not be resolved.",
        CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous => "A referenced network group is ambiguous.",
        CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed => "Referenced network groups could not be loaded.",
        CreationRuleNetworkResolutionFailureCategory.InvalidAddress => "A network address is invalid.",
        _ => "The creation-rule network input could not be resolved."
    };
}

/// <summary>Stable failure categories for creation-rule network resolution.</summary>
public enum CreationRuleNetworkResolutionFailureCategory
{
    InvalidInput,
    UnsupportedTask,
    ProtocolOrderInvalid,
    ObjectUnresolved,
    GroupUnresolved,
    GroupAmbiguous,
    GroupQueryFailed,
    InvalidAddress,
    UnexpectedFailure
}
