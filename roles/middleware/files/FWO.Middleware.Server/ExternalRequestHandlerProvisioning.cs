using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services;
using FWO.Services.Workflow;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Provisioning of a ticket along its provisioning work list, see <see cref="ProvisioningWorkList"/>.
    /// </summary>
    public partial class ExternalRequestHandler
    {
        private const string kProvisioningLogTitle = "Provisioning";
        private const string kWorkflowAlertSource = "workflow";

        /// <summary>A firewall write is handled by no external ticket system.</summary>
        private static readonly ExternalTicketSystem kNoExternalTicketSystem = new();

        /// <summary>
        /// Creates the external request for the next entry of the provisioning work list of a ticket: a request task
        /// handed to its external ticket system, or an implementation task written to the firewall. The requests of a
        /// ticket run one after another, so nothing is created while a request of the ticket is still being processed.
        /// The work list is built anew from the current tasks and settings each time, so a second call creates nothing
        /// twice and a finished entry, e.g. set to done by hand, is skipped.
        /// </summary>
        /// <param name="ticketId">Ticket to provision.</param>
        /// <param name="scopeReqTaskId">Request task in the request task scope; null in the ticket scope.</param>
        /// <param name="previousRequest">Request just finished, if the provisioning continues after it; a group change it
        /// made may let the next request wait some cycles, see <see cref="GetWaitCycles"/>, and a release by a button it
        /// carries continues.</param>
        /// <param name="byButton">True if a person pressed a button: the scope is released, see <see cref="ProvisioningRelease"/>.</param>
        /// <returns>true if a request was created or nothing is to be done; false if the provisioning failed.</returns>
        public async Task<bool> StartProvisioning(long ticketId, long? scopeReqTaskId = null, ExternalRequest? previousRequest = null, bool byButton = false)
        {
            if (IsSingleImplTaskRequest(previousRequest))
            {
                Log.WriteInfo(kProvisioningLogTitle, $"External request {previousRequest?.Id} wrote a single implementation task of ticket {ticketId}; nothing follows it.");
                return true;
            }
            try
            {
                WfTicket? ticket = await InitAndResolve(ticketId);
                if (ticket == null)
                {
                    Log.WriteWarning(kProvisioningLogTitle, $"Ticket {ticketId} not found.");
                    return false;
                }
                if (await HasUnfinishedRequest(ticket.Id))
                {
                    Log.WriteInfo(kProvisioningLogTitle, $"Ticket {ticket.Id} still has an external request being processed; the next one follows when it is finished.");
                    return true;
                }
                WfReqTask? scopeTask = null;
                if (scopeReqTaskId != null)
                {
                    scopeTask = ticket.Tasks.FirstOrDefault(task => task.Id == scopeReqTaskId);
                    if (scopeTask == null)
                    {
                        Log.WriteWarning(kProvisioningLogTitle, $"Request task {scopeReqTaskId} not found in ticket {ticket.Id}.");
                        return false;
                    }
                }
                ProvisioningRelease? release = byButton ? new(scopeTask?.TaskNumber) : ReleaseOf(previousRequest);
                ProvisioningWorkList workList = await wfHandler.BuildProvisioningWorkList(ticket, scopeTask, release);
                return await CreateRequestForNextEntry(ticket, workList, previousRequest, release);
            }
            catch (Exception exception)
            {
                Log.WriteError(kProvisioningLogTitle, $"Provisioning of ticket {ticketId} failed: ", exception);
                return false;
            }
        }

        /// <summary>
        /// Creates the external request writing a single implementation task to the firewall, see
        /// <see cref="WfHandler.BuildProvisioningOfImplTask"/>. The request is marked, so that nothing follows it.
        /// </summary>
        /// <param name="ticketId">Ticket of the implementation task.</param>
        /// <param name="implTaskId">Implementation task to write.</param>
        /// <param name="byButton">True if a person pressed a button: the task is written on a Manual target as well.</param>
        /// <returns>true if the request was created or nothing is to be done; false if the provisioning failed or another
        /// request of the ticket is still being processed, which is reported with an alert.</returns>
        public async Task<bool> ProvisionImplTask(long ticketId, long implTaskId, bool byButton = false)
        {
            try
            {
                WfTicket? ticket = await InitAndResolve(ticketId);
                (WfReqTask? reqTask, WfImplTask? implTask) = FindImplTask(ticket, implTaskId);
                if (ticket == null || reqTask == null || implTask == null)
                {
                    Log.WriteWarning(kProvisioningLogTitle, $"Implementation task {implTaskId} not found in ticket {ticketId}.");
                    return false;
                }
                if (await HasUnfinishedRequest(ticket.Id))
                {
                    await ReportImplTaskNotWritten(ticket, reqTask, implTask,
                        "another external request of the ticket is still being processed; start it again when that request is finished.");
                    return false;
                }
                ProvisioningWorkItem? item = await wfHandler.BuildProvisioningOfImplTask(ticket, implTaskId, byButton);
                if (item == null || item.Way != ProvisioningWay.FirewallWrite || item.IsFinished || item.WaitsForPlanning)
                {
                    Log.WriteInfo(kProvisioningLogTitle, $"Implementation task {implTaskId} of ticket {ticket.Id} is not written: it is finished, " +
                        "still being planned, or not written by FWO in its implementation mode.");
                    return true;
                }
                if (IsCoveredByUnfinishedTask(reqTask, implTask, item.Rule))
                {
                    Log.WriteInfo(kProvisioningLogTitle, $"Implementation task {implTask.TaskNumber} of request task {reqTask.TaskNumber} of ticket {ticket.Id} " +
                        $"is not written: its rule is written by implementation task {item.Rule!.WriterTaskNumber}, which is to be written first.");
                    return true;
                }
                await CreateFirewallWriteRequest(ticket, reqTask, implTask, 0, null, true, item.Rule);
                Log.WriteInfo(kProvisioningLogTitle, $"Created the external request for implementation task {implTask.TaskNumber} " +
                    $"of request task {reqTask.TaskNumber} of ticket {ticket.Id}.");
                return true;
            }
            catch (Exception exception)
            {
                Log.WriteError(kProvisioningLogTitle, $"Writing implementation task {implTaskId} of ticket {ticketId} failed: ", exception);
                return false;
            }
        }

        private async Task<bool> CreateRequestForNextEntry(WfTicket ticket, ProvisioningWorkList workList, ExternalRequest? previousRequest,
            ProvisioningRelease? release)
        {
            ProvisioningWorkItem? next = workList.Next;
            if (next == null)
            {
                Log.WriteInfo(kProvisioningLogTitle, $"Nothing left to provision for ticket {ticket.Id}.");
                return true;
            }
            if (next.WaitsForPlanning)
            {
                Log.WriteInfo(kProvisioningLogTitle, $"Request task {next.ReqTask.TaskNumber} of ticket {ticket.Id} is still being planned; " +
                    "its implementation tasks are written once it is planned.");
                return true;
            }
            if (next.Way == ProvisioningWay.FirewallWrite && next.ImplTask != null)
            {
                int waitCycles = GetWaitCycles(next.ImplTask.TaskType, previousRequest, kNoExternalTicketSystem);
                await CreateFirewallWriteRequest(ticket, next.ReqTask, next.ImplTask, waitCycles, release, sharedRule: next.Rule);
            }
            else
            {
                await CreateExternalTicketSystemRequest(ticket, workList, next, previousRequest, release);
            }
            Log.WriteInfo(kProvisioningLogTitle, $"Created the external request for request task {next.ReqTask.TaskNumber}" +
                $"{(next.ImplTask == null ? "" : $", implementation task {next.ImplTask.TaskNumber}")} of ticket {ticket.Id}.");
            return true;
        }

        /// <summary>
        /// Reports an implementation task that a person started to be written but that is not written now, with a
        /// workflow alert, as the person gets no other answer; the task keeps its state and can be started again.
        /// </summary>
        private async Task ReportImplTaskNotWritten(WfTicket ticket, WfReqTask reqTask, WfImplTask implTask, string reason)
        {
            string description = $"Ticket {ticket.Id}, request task {reqTask.Id}, implementation task {implTask.Id}: not written, {reason}";
            Log.WriteWarning(kProvisioningLogTitle, description);
            int? managementId = await wfHandler.ResolveImplTaskManagementId(reqTask, implTask) ?? implTask.ManagementId;
            await AlertHelper.SetAlert(ApiConnection, kProvisioningLogTitle, description, kWorkflowAlertSource, AlertCode.WorkflowAlert,
                new AlertHelper.AdditionalAlertData { MgmtId = managementId, CompareDesc = true });
        }

        /// <summary>
        /// A request without finish date is still being processed; same criterion as the unique index uidx_ext_request_one_active_per_ticket.
        /// </summary>
        private async Task<bool> HasUnfinishedRequest(long ticketId)
        {
            List<ExternalRequest> unfinishedRequests = await ApiConnection.SendQueryAsync<List<ExternalRequest>>(ExtRequestQueries.getUnfinishedRequestsOfTicket, new { ticketId });
            return unfinishedRequests.Count > 0;
        }

        /// <summary>
        /// Creates the request writing an implementation task to the firewall. It references the implementation task,
        /// which the sender loads itself, so the request carries no ticket system and no content.
        /// </summary>
        private async Task CreateFirewallWriteRequest(WfTicket ticket, WfReqTask reqTask, WfImplTask implTask, int waitCycles, ProvisioningRelease? release,
            bool singleImplTask = false, SharedRule? sharedRule = null)
        {
            Dictionary<string, List<int>> extQueryVariables = [];
            if (await wfHandler.ResolveImplTaskManagementId(reqTask, implTask) is int managementId)
            {
                extQueryVariables[ExternalVarKeys.ManagementId] = [managementId];
            }
            AddRelease(extQueryVariables, release);
            if (singleImplTask)
            {
                extQueryVariables[ExternalVarKeys.SingleImplTask] = [1];
            }
            AddSharedRule(extQueryVariables, implTask, sharedRule);
            var variables = new
            {
                ownerId = reqTask.Owners.FirstOrDefault()?.Owner.Id ?? ticket.Tasks.FirstOrDefault()?.Owners.FirstOrDefault()?.Owner.Id,
                ticketId = ticket.Id,
                taskNumber = reqTask.TaskNumber,
                extTicketSystem = "",
                extTaskType = implTask.TaskType,
                extTaskContent = "",
                extQueryVariables = JsonSerializer.Serialize(extQueryVariables),
                extRequestState = ExtStates.ExtReqInitialized.ToString(),
                waitCycles,
                implTaskId = implTask.Id
            };
            await ApiConnection.SendQueryAsync<ReturnIdWrapper>(ExtRequestQueries.addExtRequest, variables);
        }

        /// <summary>
        /// Hands a request task to Tufin SecureChange as configured for its management in the device specific settings
        /// (connection, ticket templates, response timeout and retries), together with the request tasks it is bundled with.
        /// </summary>
        /// <exception cref="InvalidOperationException">The management has no URL or no credential for Tufin SecureChange.</exception>
        private async Task CreateExternalTicketSystemRequest(WfTicket ticket, ProvisioningWorkList workList, ProvisioningWorkItem start, ExternalRequest? previousRequest,
            ProvisioningRelease? release)
        {
            actSystem = ExternalTicketSystem.FromSecureChangeSettings(await GetRequestSettings(start.ReqTask.ManagementId));
            if (string.IsNullOrWhiteSpace(actSystem.Url) || actSystem.CredentialId == null)
            {
                throw new InvalidOperationException($"The Tufin SecureChange connection of management {start.ReqTask.ManagementId} has no URL or no credential.");
            }
            await ApplySecureChangeTemplates(ticket, start.ReqTask);
            (List<WfReqTask> contentTasks, List<WfReqTask> handledTasks) = await BundleRequestTasks(workList, start);
            await CreateExtRequest(ticket, contentTasks, handledTasks, GetWaitCycles(start.ReqTask.TaskType, previousRequest, actSystem), release);
        }

        /// <summary>
        /// Bundles the open entries directly following the start entry into one request, as long as they are handed to
        /// the same external ticket system with the same connection, have the same task type and the same ticket templates.
        /// Only rule tasks are bundled. A rule change of the same connection on another gateway of the same management
        /// is merged into the task before (if the system bundles gateways); other tasks join as tasks of their own if
        /// bundling is switched on, up to the maximum number of tasks of the system. The first entry that does not fit
        /// ends the bundle; finished entries and implementation tasks left to a person do not interrupt it.
        /// </summary>
        /// <returns>The tasks forming the content of the request and all request tasks it handles.</returns>
        private async Task<(List<WfReqTask> ContentTasks, List<WfReqTask> HandledTasks)> BundleRequestTasks(ProvisioningWorkList workList, ProvisioningWorkItem start)
        {
            List<WfReqTask> contentTasks = [];
            List<WfReqTask> handledTasks = [start.ReqTask];
            WfReqTask currentTask = start.ReqTask;
            if (IsBundledTaskType(start.ReqTask.TaskType))
            {
                foreach (ProvisioningWorkItem further in OpenEntriesAfter(workList, start))
                {
                    if (!await CanBundle(further, start))
                    {
                        break;
                    }
                    if (actSystem.BundleGateways() && actSystem.TaskTypesToBundleGateways().Contains(start.ReqTask.TaskType)
                        && IsSameRuleOnDiffGw(currentTask, further.ReqTask))
                    {
                        currentTask.Elements.AddRange(further.ReqTask.GetRuleElements().ConvertAll(element => element.ToReqElement()));
                        handledTasks.Add(further.ReqTask);
                        continue;
                    }
                    // the current task and the further one must fit into the request
                    if (!UserConfig.ModRolloutBundleTasks || contentTasks.Count + 2 > actSystem.MaxBundledTasks())
                    {
                        break;
                    }
                    contentTasks.Add(currentTask);
                    currentTask = new(further.ReqTask);
                    handledTasks.Add(further.ReqTask);
                }
            }
            contentTasks.Add(currentTask);
            return (contentTasks, handledTasks);
        }

        /// <summary>
        /// Device specific settings in effect for a management, e.g. the response timeout and retries of its external
        /// requests.
        /// </summary>
        /// <param name="managementId">Management of the request task or implementation task.</param>
        public async Task<GlobalDeviceSpecificSettings> GetRequestSettings(int? managementId)
        {
            return await wfHandler.GetManagementSettings(managementId);
        }

        /// <summary>
        /// Remembers a release by a button with the request, so the provisioning keeps it when it continues afterwards.
        /// </summary>
        private static void AddRelease(Dictionary<string, List<int>> extQueryVariables, ProvisioningRelease? release)
        {
            if (release != null)
            {
                extQueryVariables[ExternalVarKeys.ButtonRelease] = [release.ScopeTaskNumber ?? 0];
            }
        }

        /// <summary>Whether a task is covered by the shared rule of another task that is not finished yet.</summary>
        private bool IsCoveredByUnfinishedTask(WfReqTask reqTask, WfImplTask implTask, SharedRule? sharedRule)
        {
            if (sharedRule == null || implTask.TaskNumber == sharedRule.WriterTaskNumber)
            {
                return false;
            }
            WfImplTask? writer = reqTask.ImplementationTasks.FirstOrDefault(task => task.TaskNumber == sharedRule.WriterTaskNumber);
            return writer != null && !IsImplTaskFinished(writer);
        }

        /// <summary>
        /// Remembers a new rule shared by several implementation tasks with the request: the writing task carries the
        /// gateways to install the rule on and the tasks it covers, a covered task the number of the writing task.
        /// </summary>
        private static void AddSharedRule(Dictionary<string, List<int>> extQueryVariables, WfImplTask implTask, SharedRule? sharedRule)
        {
            if (sharedRule == null)
            {
                return;
            }
            if (implTask.TaskNumber == sharedRule.WriterTaskNumber)
            {
                extQueryVariables[ExternalVarKeys.InstallOnGateways] = [.. sharedRule.InstallOnGatewayIds];
                extQueryVariables[ExternalVarKeys.CoveredTasks] = [.. sharedRule.CoveredTaskNumbers];
            }
            else
            {
                extQueryVariables[ExternalVarKeys.CoveredBy] = [sharedRule.WriterTaskNumber];
            }
        }

        /// <summary>The release by a button a finished request carries; null if it carries none.</summary>
        private static ProvisioningRelease? ReleaseOf(ExternalRequest? request)
        {
            List<int>? scope = ExtQueryVariable(request, ExternalVarKeys.ButtonRelease);
            if (scope == null || scope.Count == 0)
            {
                return null;
            }
            return new ProvisioningRelease(scope[0] > 0 ? scope[0] : null);
        }

        /// <summary>Whether a request wrote a single implementation task started by its own action, which nothing follows.</summary>
        private static bool IsSingleImplTaskRequest(ExternalRequest? request)
        {
            return ExtQueryVariable(request, ExternalVarKeys.SingleImplTask) != null;
        }

        /// <summary>Reads one variable of the query variables of a request, see <see cref="ExternalVarKeys"/>.</summary>
        /// <param name="request">The external request; null gives no value.</param>
        /// <param name="key">The variable to read.</param>
        /// <returns>The values of the variable; null if the request has no such variable or its variables cannot be read.</returns>
        internal static List<int>? ExtQueryVariable(ExternalRequest? request, string key)
        {
            if (string.IsNullOrWhiteSpace(request?.ExtQueryVariables))
            {
                return null;
            }
            Dictionary<string, List<int>>? extQueryVariables = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(request.ExtQueryVariables);
            return extQueryVariables != null && extQueryVariables.TryGetValue(key, out List<int>? values) ? values : null;
        }

        private static IEnumerable<ProvisioningWorkItem> OpenEntriesAfter(ProvisioningWorkList workList, ProvisioningWorkItem start)
        {
            return workList.Items.SkipWhile(item => item != start).Skip(1).Where(item => !item.IsFinished);
        }

        /// <summary>
        /// Whether a further entry joins the request of the start entry: one request goes to one SecureChange server with
        /// one login and one set of ticket templates.
        /// </summary>
        private async Task<bool> CanBundle(ProvisioningWorkItem further, ProvisioningWorkItem start)
        {
            if (further.Way != ProvisioningWay.ExternalTicketSystem || further.ExternalTicketSystem != start.ExternalTicketSystem
                || further.ReqTask.TaskType != start.ReqTask.TaskType
                || !scTemplates.HaveSameTemplates(further.ReqTask.ManagementId, start.ReqTask.ManagementId))
            {
                return false;
            }
            GlobalDeviceSpecificSettings furtherSettings = await GetRequestSettings(further.ReqTask.ManagementId);
            return furtherSettings.ScUrl == actSystem.Url && furtherSettings.ScCredential == actSystem.CredentialId;
        }

        private static bool IsBundledTaskType(string taskType)
        {
            return taskType == WfTaskType.access.ToString() || WfTaskBase.IsRuleChangeOrDelete(taskType);
        }
    }
}
