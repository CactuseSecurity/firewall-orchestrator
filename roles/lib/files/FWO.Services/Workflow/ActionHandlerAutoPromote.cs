using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Logging;
using System.Globalization;
using System.Text.Json;


namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        /// <summary>
        /// Text key of the ticket comment written when an object is moved into the error state after a timeout.
        /// </summary>
        public const string kAutoPromoteTimeoutTextKey = "auto_promote_timeout";

        private const string kAutoPromoteLogTitle = "AutoPromote";
        private const string kCommentDateFormat = "yyyy-MM-dd HH:mm:ss";
        private const string kCommentSeparator = " ";

        /// <summary>
        /// Clock used to measure how long an object has been in its state.
        /// </summary>
        public TimeProvider Clock { get; set; } = TimeProvider.System;

        private async Task PerformAutoPromote(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            AutoPromoteDecision decision = await GetAutoPromoteDecision(action.ExternalParams, statefulObject, scope);
            if (decision.RemainsInState)
            {
                LogRemainInState(action, decision, statefulObject, scope);
                return;
            }

            int? toState = decision.ToStateId;
            if (toState != null && !states.Any(state => state.Id == toState))
            {
                return;
            }

            if (!await wfHandler.AutoPromote(statefulObject, scope, toState))
            {
                // nothing was moved (e.g. the ticket was changed concurrently), so there is nothing to document
                return;
            }
            await AddAutoPromoteComment(decision.TicketComment);
            if (WfStateAction.HasConfirmUiMessage(action.ExternalParams))
            {
                wfHandler.DisplayMessage(null, action.Name, BuildAutoPromoteMessage(statefulObject), false);
            }
        }

        private async Task<AutoPromoteDecision> GetAutoPromoteDecision(string externalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (!WfStateAction.TryParseAutoPromoteParams(externalParams, out int? toState, out ConditionalAutoPromoteParams? conditionalParams))
            {
                throw new JsonException("Extparams could not be parsed.");
            }

            if (conditionalParams == null)
            {
                return new AutoPromoteDecision(toState);
            }

            ConditionalCheckResult checkResult = await EvaluateConditionalAutoPromote(conditionalParams, statefulObject, scope);
            if (checkResult.Passed)
            {
                return ConditionalDecision(conditionalParams.IfCompliantState, checkResult.TicketComment, statefulObject);
            }

            AutoPromoteDecision? errorDecision = await CheckErrorConditions(conditionalParams, checkResult, statefulObject, scope);
            return errorDecision ?? ConditionalDecision(conditionalParams.IfNotCompliantState, checkResult.TicketComment, statefulObject);
        }

        /// <summary>
        /// A conditional target equal to the current state keeps the object where it is, so that a periodically
        /// performed action neither moves the object into its own state nor comments the ticket on every run.
        /// The target "automatic" is not supported for conditional targets; configurations saved with it keep the
        /// object where it is as well, and the decision is marked so that the misconfiguration is logged.
        /// </summary>
        private static AutoPromoteDecision ConditionalDecision(int targetState, string? ticketComment, WfStatefulObject statefulObject)
        {
            if (targetState == WfStateAction.AutomaticStateId)
            {
                return AutoPromoteDecision.RemainForAutomaticTarget;
            }

            return targetState == statefulObject.StateId
                ? AutoPromoteDecision.Remain
                : new AutoPromoteDecision(targetState, ticketComment);
        }

        /// <summary>
        /// Logs that the object stays in its state: as warning for the unsupported conditional target "automatic",
        /// so that the misconfigured action becomes visible, otherwise as debug message.
        /// </summary>
        private void LogRemainInState(WfStateAction action, AutoPromoteDecision decision, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            long objectId = GetStatefulObjectId(statefulObject, scope);
            if (decision.HasAutomaticTarget)
            {
                Log.WriteWarning(kAutoPromoteLogTitle, $"Action '{action.Name}' has the conditional target \"automatic\", which is not supported. " +
                    $"{scope} {objectId} remains in state {statefulObject.StateId}. Select a state or \"Remain in state\" as target.");
                return;
            }
            Log.WriteDebug(kAutoPromoteLogTitle, $"Action '{action.Name}' keeps {scope} {objectId} in state {statefulObject.StateId}.");
        }

        /// <summary>
        /// Checks the error conditions after the check did not pass. The first condition that applies moves the
        /// object into the error state instead of the target for a check that did not pass.
        /// </summary>
        /// <param name="conditionalParams">Parameters of the conditional auto-forward.</param>
        /// <param name="checkResult">Result of the check that did not pass; its comment is kept in the error comment.</param>
        /// <param name="statefulObject">Object the action is performed for.</param>
        /// <param name="scope">Scope of the object.</param>
        /// <returns>The decision for the error state, or null when no error state is set or no condition applies.</returns>
        private async Task<AutoPromoteDecision?> CheckErrorConditions(ConditionalAutoPromoteParams conditionalParams, ConditionalCheckResult checkResult,
            WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (conditionalParams.ErrorState is not int errorState)
            {
                return null;
            }

            string? timeoutComment = await CheckTimeout(conditionalParams.ErrorConditions, statefulObject, scope);
            return timeoutComment == null
                ? null
                : ConditionalDecision(errorState, CombineComments(timeoutComment, checkResult.TicketComment), statefulObject);
        }

        /// <summary>
        /// Joins the comment documenting an error condition and the comment of the check that did not pass,
        /// e.g. the list of missing managements, into one ticket comment.
        /// </summary>
        private static string CombineComments(string errorComment, string? checkComment)
        {
            return string.IsNullOrWhiteSpace(checkComment)
                ? errorComment
                : errorComment + kCommentSeparator + checkComment;
        }

        /// <summary>
        /// Checks whether the object has been in its state for longer than the configured timeout.
        /// </summary>
        /// <returns>The ticket comment documenting the timeout, or null when no timeout applies.</returns>
        private async Task<string?> CheckTimeout(ConditionalErrorConditions errorConditions, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (!errorConditions.HasTimeout)
            {
                return null;
            }

            DateTime? stateEntry = await GetStateEntryTime(statefulObject, scope);
            if (stateEntry is not DateTime enteredAt)
            {
                return null;
            }

            int timeoutMinutes = errorConditions.TimeoutMinutes!.Value;
            TimeSpan timeInState = Clock.GetUtcNow().UtcDateTime - enteredAt.ToUniversalTime();
            if (timeInState < TimeSpan.FromMinutes(timeoutMinutes))
            {
                return null;
            }

            string enteredAtText = enteredAt.ToLocalTime().ToString(kCommentDateFormat, CultureInfo.InvariantCulture);
            Log.WriteInfo(kAutoPromoteLogTitle, $"{scope} {GetStatefulObjectId(statefulObject, scope)} is in state {statefulObject.StateId} " +
                $"since {enteredAtText}, longer than the timeout of {timeoutMinutes} minutes.");
            return string.Format(CultureInfo.InvariantCulture, wfHandler.userConfig.GetText(kAutoPromoteTimeoutTextKey), timeoutMinutes, enteredAtText);
        }

        /// <summary>
        /// Returns the time the object entered its current state, taken from the state change the middleware
        /// recorded when it executed the state actions.
        /// </summary>
        /// <returns>The time, or null when no state change into the current state is recorded.</returns>
        private async Task<DateTime?> GetStateEntryTime(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            long objectId = GetStatefulObjectId(statefulObject, scope);
            if (objectId <= 0)
            {
                return null;
            }

            try
            {
                List<WfStateChangeExecution> executions = await apiConnection.SendQueryAsync<List<WfStateChangeExecution>>(
                    RequestQueries.getStateChangeExecution, new { objectScope = scope.ToString(), objectId }) ?? [];
                WfStateChangeExecution? execution = executions.FirstOrDefault(candidate => candidate.ToStateId == statefulObject.StateId);
                if (execution == null)
                {
                    Log.WriteDebug(kAutoPromoteLogTitle, $"No recorded state change of {scope} {objectId} into state {statefulObject.StateId}.");
                    return null;
                }
                return execution.ExecutedAt.ToLocalTime();
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kAutoPromoteLogTitle, $"State entry time of {scope} {objectId} could not be read: {exception.Message}");
                return null;
            }
        }

        private async Task AddAutoPromoteComment(string? commentText)
        {
            if (string.IsNullOrWhiteSpace(commentText) || wfHandler.ActTicket.Id <= 0)
            {
                return;
            }
            await wfHandler.ConfAddCommentToTicket(commentText);
        }

        private async Task<ConditionalCheckResult> EvaluateConditionalAutoPromote(ConditionalAutoPromoteParams conditionalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            return conditionalParams.ToBeCalled switch
            {
                ToBeCalled.PolicyCheck => new ConditionalCheckResult(
                    await ExecutePolicyCheck(conditionalParams.PolicyIds, conditionalParams.CheckResultLabel, statefulObject, scope)),
                ToBeCalled.RolloutCheck => await ExecuteRolloutCheck(conditionalParams.CheckResultLabel, statefulObject, scope),
                _ => new ConditionalCheckResult(false)
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
                    if (policyCheckTasks.Count > 0)
                    {
                        Log.WriteInfo("Policy Check", "No policy-checkable rule tasks were found; treating the conditional policy check as compliant.");
                        return true;
                    }

                    Log.WriteWarning("Policy Check", "No request tasks were available for the conditional policy check.");
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
                await AttachCheckResultLabel(requestedRuleTasks, checkResultLabel, isCompliant);
                return isCompliant;
            }
            catch (Exception exc)
            {
                Log.WriteError("Policy Check", "Conditional compliance evaluation failed.", exc);
                return false;
            }
        }

        /// <summary>
        /// Stores the check result under the label in the additional info of the request tasks. Tasks already
        /// carrying the result are left alone, so that a periodically performed check does not write them again.
        /// </summary>
        private async Task AttachCheckResultLabel(IEnumerable<WfReqTask> requestTasks, string checkResultLabel, bool passed)
        {
            if (string.IsNullOrWhiteSpace(checkResultLabel))
            {
                return;
            }

            string label = checkResultLabel.Trim();
            string result = passed.ToString().ToLowerInvariant();
            List<WfReqTask> changedTasks = [.. requestTasks.Where(task => task.GetAddInfoValue(label) != result)];
            if (changedTasks.Count > 0)
            {
                await wfHandler.SetAddInfoInReqTasks(changedTasks, label, result);
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
                    || task.TaskType == WfTaskType.group_modify.ToString()
                    || task.TaskType == WfTaskType.object_create.ToString()
                    || task.TaskType == WfTaskType.object_modify.ToString()
                    || (string.Equals(task.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase)
                        && task.GetRuleElements().Count > 0)))
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
