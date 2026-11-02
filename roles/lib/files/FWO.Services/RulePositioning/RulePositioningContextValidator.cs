using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Services.RulePositioning;

/// <summary>Central validation rules for workflow and direct rule-positioning entry points.</summary>
public static class RulePositioningContextValidator
{
    /// <summary>Checks task shape and identity before external context is loaded.</summary>
    public static bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask)
    {
        if (requestTask == null
            || implementationTask == null
            || requestTask.Id <= 0
            || implementationTask.Id <= 0
            || implementationTask.ReqTaskId != requestTask.Id
            || implementationTask.DeviceId is null or <= 0
            || EffectiveManagementId(requestTask, implementationTask) <= 0
            || !IsNewAccessRule(implementationTask, requestTask))
        {
            return false;
        }

        List<int> requestGatewayIds = [.. requestTask.GetDeviceList().Distinct()];
        bool hasExplicitFiniteGatewaySelection = requestGatewayIds.Count > 0
            && !requestGatewayIds.Contains(WfReqTaskBase.kAllDevicesId);
        return (!hasExplicitFiniteGatewaySelection
                || requestGatewayIds.Contains(implementationTask.DeviceId.Value))
            && (requestTask.OnManagement == null
                || requestTask.OnManagement.Id == requestTask.ManagementId);
    }

    /// <summary>Returns the implementation override or request-task management.</summary>
    public static int EffectiveManagementId(WfReqTask requestTask, WfImplTask implementationTask) =>
        implementationTask.ManagementId ?? requestTask.ManagementId ?? 0;

    /// <summary>Validates the complete manager contract, including workflow identity relationships.</summary>
    public static RulePositioningFailureCategory? ValidateManagerContext(RulePositioningContext? context)
    {
        if (context == null
            || context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetManagement == null
            || context.TargetGateway.DeviceType == null
            || context.TargetGateway.DeviceType.Id <= 0)
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        if (!IsNewAccessRule(context.ImplementationTask, context.ParentRequestTask))
        {
            return RulePositioningFailureCategory.UnsupportedTask;
        }
        if (!HasConsistentTaskAndTargetReferences(context))
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        return HasUsableConfiguration(context.EffectiveSettings)
            ? null
            : RulePositioningFailureCategory.ConfigurationInvalid;
    }

    /// <summary>Validates the inputs required by graph-only direct strategy invocation.</summary>
    public static RulePositioningFailureCategory? ValidateGraphContext(RulePositioningContext? context)
    {
        RulePositioningFailureCategory? common = ValidateDirectContext(context, requireImplementationTask: true);
        return common ?? ValidateAlgorithm(context!.EffectiveSettings);
    }

    /// <summary>Validates the inputs required by ticket-aware direct strategy invocation.</summary>
    public static RulePositioningFailureCategory? ValidateTicketAwareContext(RulePositioningContext? context)
    {
        RulePositioningFailureCategory? common = ValidateDirectContext(context, requireImplementationTask: false);
        if (common != null)
        {
            return common;
        }
        if (context!.TicketRequestTasks == null || context.Configuration == null)
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        return ValidateAlgorithm(context.EffectiveSettings);
    }

    private static RulePositioningFailureCategory? ValidateDirectContext(
        RulePositioningContext? context,
        bool requireImplementationTask)
    {
        return context == null
            || requireImplementationTask && context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetGateway.DeviceType == null
            || context.TargetManagement == null
            || context.EffectiveSettings == null
            || context.TargetManagement.Id <= 0
            || context.TargetGateway.Id <= 0
                ? RulePositioningFailureCategory.InvalidInput
                : null;
    }

    private static RulePositioningFailureCategory? ValidateAlgorithm(GatewayDeviceSpecificSettings settings) =>
        HasUsableConfiguration(settings)
            ? null
            : RulePositioningFailureCategory.ConfigurationInvalid;

    private static bool IsNewAccessRule(WfImplTask implementationTask, WfReqTask requestTask)
    {
        string accessTaskType = WfTaskType.access.ToString();
        string createAction = RequestAction.create.ToString();
        return implementationTask.TaskType == accessTaskType
            && requestTask.TaskType == accessTaskType
            && implementationTask.ImplAction == createAction
            && requestTask.RequestAction == createAction;
    }

    private static bool HasConsistentTaskAndTargetReferences(RulePositioningContext context)
    {
        if (context.ImplementationTask.Id <= 0
            || context.ImplementationTask.ReqTaskId <= 0
            || context.ImplementationTask.DeviceId is null or <= 0
            || context.ParentRequestTask.Id <= 0
            || context.ImplementationTask.ReqTaskId != context.ParentRequestTask.Id)
        {
            return false;
        }

        List<int> requestGatewayIds = [.. context.ParentRequestTask.GetDeviceList().Distinct()];
        bool hasExplicitFiniteGatewaySelection = requestGatewayIds.Count > 0
            && !requestGatewayIds.Contains(WfReqTaskBase.kAllDevicesId);
        if (hasExplicitFiniteGatewaySelection
            && !requestGatewayIds.Contains(context.ImplementationTask.DeviceId.Value))
        {
            return false;
        }
        if (context.TargetGateway.Id != context.ImplementationTask.DeviceId.Value)
        {
            return false;
        }

        int effectiveManagementId = EffectiveManagementId(context.ParentRequestTask, context.ImplementationTask);
        if (effectiveManagementId <= 0
            || context.TargetManagement.Id != effectiveManagementId
            || context.TargetGateway.Management == null
            || context.TargetGateway.Management.Id != effectiveManagementId)
        {
            return false;
        }
        return context.ParentRequestTask.OnManagement == null
            || context.ParentRequestTask.OnManagement.Id == context.ParentRequestTask.ManagementId;
    }

    private static bool HasUsableConfiguration(GatewayDeviceSpecificSettings? settings)
    {
        if (settings == null)
        {
            return false;
        }
        ProvisioningPositioningAlgorithm algorithm = settings.PositioningAlgorithm;
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }
}
