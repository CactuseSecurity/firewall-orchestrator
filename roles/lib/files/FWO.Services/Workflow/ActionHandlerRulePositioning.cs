using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.RulePositioning;

namespace FWO.Services.Workflow;

/// <summary>Rule-positioning activation owned by the workflow action handler.</summary>
public partial class ActionHandler
{
    private readonly IRulePositioningWorkflowCaller? rulePositioningWorkflowCaller;

    internal TimeSpan RulePositioningTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Attempts positioning for one durable implementation-task creation event.</summary>
    private async Task ActivateRulePositioningForCreation(
        WfStatefulObject statefulObject,
        WfObjectScopes scope,
        WfReqTask? creatingRequestTask)
    {
        if (!useInMwServer
            || scope != WfObjectScopes.ImplementationTask
            || statefulObject is not WfImplTask implementationTask
            || !implementationTask.StateChangedByCreation())
        {
            return;
        }

        try
        {
            if (rulePositioningWorkflowCaller == null)
            {
                LogRulePositioningActivationFailure(implementationTask.Id);
                return;
            }

            WfReqTask? requestTask = creatingRequestTask ?? wfHandler.ResolveRulePositioningRequestTask(implementationTask);
            if (requestTask == null || requestTask.Id != implementationTask.ReqTaskId)
            {
                LogRulePositioningActivationFailure(implementationTask.Id);
                return;
            }

            if (rulePositioningWorkflowCaller.IsApplicable(requestTask, implementationTask))
            {
                using CancellationTokenSource timeout = new(RulePositioningTimeout);
                await rulePositioningWorkflowCaller.CalculateAndPersistAsync(
                    requestTask,
                    implementationTask,
                    timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            LogRulePositioningActivationFailure(implementationTask.Id);
        }
        catch (Exception)
        {
            LogRulePositioningActivationFailure(implementationTask.Id);
        }
    }

    /// <summary>Logs a fixed, payload-free positioning activation failure.</summary>
    private static void LogRulePositioningActivationFailure(long implementationTaskId)
    {
        Log.WriteWarning("Rule positioning activation", $"Activation failed for implementation task {implementationTaskId}.");
    }
}
