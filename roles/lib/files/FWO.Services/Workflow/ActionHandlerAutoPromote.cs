using FWO.Data.Workflow;
using FWO.Logging;
using System.Text.Json;


namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        private async Task<int?> GetAutoPromoteTargetState(string externalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (!WfStateAction.TryParseAutoPromoteParams(externalParams, out int? toState, out ConditionalAutoPromoteParams? conditionalParams))
            {
                throw new JsonException("Extparams could not be parsed.");
            }

            if (conditionalParams == null)
            {
                return toState;
            }

            return await EvaluateConditionalAutoPromote(conditionalParams, statefulObject, scope) ? conditionalParams.IfCompliantState : conditionalParams.IfNotCompliantState;
        }

        private Task<bool> EvaluateConditionalAutoPromote(ConditionalAutoPromoteParams conditionalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            return conditionalParams.ToBeCalled switch
            {
                ToBeCalled.PolicyCheck => ExecutePolicyCheck(conditionalParams.PolicyIds, conditionalParams.CheckResultLabel, statefulObject, scope),
                _ => Task.FromResult(false)
            };
        }

        private async Task<bool> ExecutePolicyCheck(IEnumerable<int> selectedPolicyIds, string checkResultLabel, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            try
            {
                List<WfReqTask> requestedRuleTasks = GetRequestedRuleTasksForCallingTicket(statefulObject, scope);
                if (requestedRuleTasks.Count == 0)
                {
                    return false;
                }

                if (requestedRulePolicyChecker == null)
                {
                    return false;
                }

                bool isCompliant = await requestedRulePolicyChecker.AreRequestTasksCompliant(selectedPolicyIds, requestedRuleTasks);
                await AttachPolicyCheckResultLabel(requestedRuleTasks, checkResultLabel, isCompliant);
                return isCompliant;
            }
            catch (Exception exc)
            {
                Log.WriteError("Policy Check", "Conditional compliance evaluation failed.", exc);
                return false;
            }
        }

        private async Task AttachPolicyCheckResultLabel(IEnumerable<WfReqTask> requestTasks, string checkResultLabel, bool isCompliant)
        {
            if (string.IsNullOrWhiteSpace(checkResultLabel))
            {
                return;
            }

            foreach (WfReqTask requestTask in requestTasks)
            {
                await wfHandler.SetAddInfoInReqTask(requestTask, checkResultLabel.Trim(), isCompliant.ToString().ToLowerInvariant());
            }
        }

        private List<WfReqTask> GetRequestedRuleTasksForCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            WfTicket? ticket = GetCallingTicket(statefulObject, scope);
            if (ticket == null)
            {
                return [];
            }

            return ticket.Tasks
                .Where(task => task.ManagementId != null)
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0)
                .ToList();
        }

        private WfTicket? GetCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (scope == WfObjectScopes.Ticket && statefulObject is WfTicket ticket)
            {
                return ticket;
            }

            if (wfHandler.ActTicket.Tasks.Count > 0)
            {
                return wfHandler.ActTicket;
            }

            return scope switch
            {
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => new WfTicket { Tasks = [reqTask] },
                WfObjectScopes.ImplementationTask when wfHandler.ActReqTask.Id > 0 => new WfTicket { Tasks = [wfHandler.ActReqTask] },
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => new WfTicket { Tasks = [wfHandler.ActReqTask] },
                _ => null
            };
        }
    }
}
