using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

public interface ICreationRuleNetworkInputResolver
{
    /// <summary>Resolves the effective source and destination address ranges at the access task.</summary>
    Task<CreationRuleNetworkResolutionResult> ResolveAsync(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks,
        CancellationToken cancellationToken);
}

public abstract class CreationRuleNetworkResolutionResult;

public sealed class CreationRuleNetworkResolutionSuccess(CreationRuleNetworkInput input)
    : CreationRuleNetworkResolutionResult
{
    public CreationRuleNetworkInput Input { get; } = input ?? throw new ArgumentNullException(nameof(input));
}

public sealed class CreationRuleNetworkResolutionFailure(CreationRuleNetworkResolutionFailureCategory category)
    : CreationRuleNetworkResolutionResult
{
    public CreationRuleNetworkResolutionFailureCategory Category { get; } = category;
    public string Diagnostic { get; } = category switch
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
