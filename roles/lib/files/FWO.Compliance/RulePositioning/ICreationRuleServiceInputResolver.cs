using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves effective creation-rule services from workflow state.</summary>
public interface ICreationRuleServiceInputResolver
{
    /// <summary>Resolves the effective services at the access-rule task.</summary>
    Task<PositioningResult<CreationRuleServiceInput, CreationRuleServiceResolutionFailureCategory>> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken);
}

/// <summary>Safe diagnostics for creation-rule service-resolution failures.</summary>
public static class CreationRuleServiceResolutionFailureDiagnostics
{
    /// <summary>Returns the fixed safe diagnostic for a service-resolution failure.</summary>
    public static string GetDiagnostic(this CreationRuleServiceResolutionFailureCategory category) => category switch
    {
        CreationRuleServiceResolutionFailureCategory.InvalidInput => "The creation-rule service input is invalid.",
        CreationRuleServiceResolutionFailureCategory.UnsupportedTask => "The request task is not a supported access-rule creation.",
        CreationRuleServiceResolutionFailureCategory.ProtocolOrderInvalid => "A referenced service change is ordered after the access rule.",
        CreationRuleServiceResolutionFailureCategory.ServiceUnresolved => "A referenced service could not be resolved.",
        CreationRuleServiceResolutionFailureCategory.GroupUnresolved => "A referenced service group could not be resolved.",
        CreationRuleServiceResolutionFailureCategory.GroupAmbiguous => "A referenced service group is ambiguous.",
        CreationRuleServiceResolutionFailureCategory.GroupQueryFailed => "Referenced service groups could not be loaded.",
        CreationRuleServiceResolutionFailureCategory.InvalidService => "A service definition is invalid.",
        _ => "The creation-rule service input could not be resolved."
    };
}

/// <summary>Stable failure categories for creation-rule service resolution.</summary>
public enum CreationRuleServiceResolutionFailureCategory
{
    InvalidInput,
    UnsupportedTask,
    ProtocolOrderInvalid,
    ServiceUnresolved,
    GroupUnresolved,
    GroupAmbiguous,
    GroupQueryFailed,
    InvalidService,
    UnexpectedFailure
}
