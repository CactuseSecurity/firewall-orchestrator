using FWO.Api.Client.Queries;
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
                List<WfReqTask> policyCheckTasks = await GetRequestedRuleTasksForCallingTicket(statefulObject, scope);
                List<WfReqTask> requestedRuleTasks = policyCheckTasks.Where(IsPolicyCheckRuleTask).ToList();
                if (requestedRuleTasks.Count == 0)
                {
                    Log.WriteWarning("Policy Check", "No eligible request-rule tasks were found for the conditional policy check.");
                    return false;
                }

                if (requestedRulePolicyChecker == null)
                {
                    Log.WriteDebug("Policy Check", "No policy checker is attached to the action handler. Resolving the configured policy checker factory.");
                    requestedRulePolicyChecker = (ServiceProvider.Services?.GetService(typeof(IRequestedRulePolicyCheckerFactory))
                        as IRequestedRulePolicyCheckerFactory)?.Create(wfHandler.userConfig, apiConnection, wfHandler.MiddlewareClient);
                }
                if (requestedRulePolicyChecker == null)
                {
                    Log.WriteWarning("Policy Check", "No requested-rule policy checker factory is registered. Policy check cannot be executed.");
                    return false;
                }

                bool isCompliant = await requestedRulePolicyChecker.AreRequestTasksCompliant(selectedPolicyIds, policyCheckTasks);
                Log.WriteDebug("Policy Check", $"Requested-rule policy check completed with result '{isCompliant}'.");
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

        private async Task<List<WfReqTask>> GetRequestedRuleTasksForCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            WfTicket? ticket = await GetCallingTicket(statefulObject, scope);
            if (ticket == null)
            {
                return [];
            }

            return ticket.Tasks
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0)
                .Concat(ticket.Tasks.Where(task =>
                    task.TaskType == WfTaskType.group_create.ToString()
                    || task.TaskType == WfTaskType.group_modify.ToString()))
                .Distinct()
                .ToList();
        }

        private static bool IsPolicyCheckRuleTask(WfReqTask task)
        {
            return task.GetNwObjectElements(ElemFieldType.source).Count > 0
                && task.GetNwObjectElements(ElemFieldType.destination).Count > 0
                && task.GetServiceElements().Count > 0;
        }

        private async Task<WfTicket?> GetCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (scope == WfObjectScopes.Ticket && statefulObject is WfTicket ticket)
            {
                return ticket;
            }

            if (wfHandler.ActTicket.Tasks.Count > 0)
            {
                return wfHandler.ActTicket;
            }

            WfReqTask? requestTask = scope switch
            {
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => reqTask,
                WfObjectScopes.ImplementationTask when wfHandler.ActReqTask.Id > 0 => wfHandler.ActReqTask,
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => wfHandler.ActReqTask,
                _ => null
            };
            if (requestTask?.TicketId > 0)
            {
                try
                {
                    WfTicket? fullTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById,
                        new { id = requestTask.TicketId });
                    if (fullTicket?.Id > 0)
                    {
                        fullTicket.UpdateCidrsInTaskElements();
                        return fullTicket;
                    }
                    return new WfTicket { Tasks = [requestTask] };
                }
                catch (Exception exc)
                {
                    Log.WriteWarning("Policy Check", $"Could not load full ticket {requestTask.TicketId} for policy check. Falling back to the current request task. {exc.Message}");
                }
            }

            return requestTask == null
                ? null
                : new WfTicket { Tasks = [requestTask] };
        }
    }
}
