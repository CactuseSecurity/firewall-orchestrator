using System.Text.Json;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Middleware.Client;

namespace FWO.Services.Workflow
{
    public class TicketCreator
    {
        private readonly WfHandler wfHandler;
        private readonly UserConfig userConfig;
        private readonly ApiConnection apiConnection;
        private int stateId;
        private string ticketTitle = "";
        private string ticketReason = "";
        private string taskTitle = "";
        private string taskReason = "";
        private int priority;


        public TicketCreator(ApiConnection apiConnection, UserConfig userConfig, System.Security.Claims.ClaimsPrincipal authUser, MiddlewareClient middlewareClient,
            WorkflowPhases phase = WorkflowPhases.request, IRequestedRulePolicyChecker? requestedRulePolicyChecker = null,
            Action<Exception?, string, string, bool>? displayMessage = null)
        {
            wfHandler = new(displayMessage ?? LogMessage, userConfig, authUser, apiConnection, middlewareClient, phase, requestedRulePolicyChecker);
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
        }

        public async Task<WfTicket> CreateTicket(FwoOwner owner, List<WfReqTask> reqTasks, string title, int? stateId, string reason = "")
        {
            await wfHandler.Init();
            stateId ??= wfHandler.MasterStateMatrix.LowestEndState;
            await wfHandler.SelectTicket(new WfTicket()
            {
                StateId = (int)stateId,
                Title = title,
                Requester = userConfig.User,
                Reason = reason,
                Locked = true
            },
                ObjAction.add);
            foreach (var reqTask in reqTasks)
            {
                wfHandler.SelectReqTask(new WfReqTask()
                {
                    StateId = (int)stateId,
                    Title = reqTask.Title,
                    TaskNumber = reqTask.TaskNumber,
                    TaskType = reqTask.TaskType,
                    Owners = [new() { Owner = owner }],
                    Reason = reqTask.Reason,
                    ManagementId = reqTask.ManagementId,
                    OnManagement = reqTask.OnManagement,
                    Elements = reqTask.Elements,
                    Comments = reqTask.Comments,
                    AdditionalInfo = reqTask.AdditionalInfo,
                    Locked = true
                },
                    ObjAction.add);
                await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams() { StateId = (int)stateId }));
                wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            }
            wfHandler.AddTicketMode = true;
            wfHandler.ActTicket.UpdateCidrsInTaskElements();
            long ticketId = await wfHandler.SaveTicket(wfHandler.ActTicket);
            if (ticketId > 0)
            {
                foreach (var reqtask in reqTasks.Where(t => t.Comments.Count > 0))
                {
                    WfReqTask? reqTaskToChange = wfHandler.ActTicket.Tasks.FirstOrDefault(x => x.TaskType == reqtask.TaskType &&
                        x.ManagementId == reqtask.ManagementId && x.Title == reqtask.Title && x.TaskNumber == reqtask.TaskNumber);
                    if (reqTaskToChange != null)
                    {
                        wfHandler.SetReqTaskEnv(reqTaskToChange);
                        await wfHandler.ConfAddCommentToReqTask(reqtask.Comments[0].Comment.CommentText);
                    }
                }
            }
            return wfHandler.ActTicket;
        }

        public async Task<bool> PromoteTicket(long ticketId, string extReqState)
        {
            try
            {
                await wfHandler.Init();
                WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
                if (ticket != null)
                {
                    ExtStateHandler extStateHandler = new(apiConnection);
                    ticket.StateId = extStateHandler.GetInternalStateId(extReqState) ?? throw new ArgumentException($"No translation defined for external state {extReqState}.");
                    return await wfHandler.PromoteTicketAndTasks(ticket);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("promote_ticket"), "leads to exception: ", exception);
            }
            return false;
        }

        public async Task<long> CreateRequestNewInterfaceTicket(FwoOwner owner, FwoOwner requestingOwner, string interfaceName, string reason = "")
        {
            await wfHandler.Init();
            stateId = wfHandler.MasterStateMatrix.LowestEndState;
            await wfHandler.SelectTicket(new WfTicket()
            {
                StateId = stateId,
                Title = userConfig.ModReqTicketTitle + ": " + interfaceName,
                Requester = userConfig.User,
                Reason = reason,
                Locked = true
            },
                ObjAction.add);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ReqOwner, requestingOwner.Id.ToString() } };
            wfHandler.SelectReqTask(new WfReqTask()
            {
                StateId = stateId,
                Title = userConfig.ModReqTaskTitle + ": " + interfaceName,
                TaskType = WfTaskType.new_interface.ToString(),
                Owners = [new() { Owner = owner }],
                Reason = reason,
                AdditionalInfo = JsonSerializer.Serialize(addInfo),
                Locked = true
            },
                ObjAction.add);
            await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams() { StateId = wfHandler.MasterStateMatrix.LowestEndState }));
            wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            wfHandler.AddTicketMode = true;
            long ticketId = await wfHandler.SaveTicket(wfHandler.ActTicket);
            if (ticketId > 0)
            {
                await AddRequesterInfoToImplTask(ticketId);
            }
            return ticketId;
        }

        public async Task SetInterfaceId(long ticketId, long connId)
        {
            await wfHandler.Init();
            WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
            if (ticket != null)
            {
                WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(x => x.TaskType == WfTaskType.new_interface.ToString());
                if (reqTask != null)
                {
                    await wfHandler.SetAddInfoInReqTask(reqTask, AdditionalInfoKeys.ConnId, connId.ToString());
                }
            }
        }

        /// <summary>
        /// Promotes the first new-interface implementation task of the given ticket.
        /// </summary>
        public async Task<bool> PromoteNewInterfaceImplTask(long ticketId, ExtStates extState, string comment = "")
        {
            return await PromoteNewInterfaceImplTasks(ticketId, extState, null, comment) > 0;
        }

        /// <summary>
        /// Promotes selected new-interface implementation tasks of the given ticket.
        /// </summary>
        public async Task<int> PromoteNewInterfaceImplTasks(long ticketId, ExtStates extState, IEnumerable<long>? requestTaskIds, string comment = "")
        {
            ExtStateHandler extStateHandler = new(apiConnection);
            await wfHandler.Init();
            List<WfImplTask> implTasks = await FindNewInterfaceImplTasks(ticketId, requestTaskIds);
            int? newState = extStateHandler.GetInternalStateId(extState);
            if (newState == null)
            {
                return 0;
            }

            int promotedCount = 0;
            foreach (WfImplTask implTask in implTasks)
            {
                await wfHandler.ContinueImplPhase(implTask);
                if (comment != "")
                {
                    await wfHandler.ConfAddCommentToImplTask(comment);
                }
                await wfHandler.PromoteImplTask(new() { StateId = (int)newState });
                promotedCount++;
            }
            return promotedCount;
        }

        public async Task CreateDecertRuleDeleteTicket(int deviceId, List<string> ruleUids, string comment = "", DateTime? deadline = null)
        {
            stateId = userConfig.RecDeleteRuleInitState;
            ticketTitle = userConfig.RecDeleteRuleTicketTitle + " ";
            ticketReason = userConfig.RecDeleteRuleTicketReason + " " + comment;
            taskTitle = userConfig.RecDeleteRuleReqTaskTitle;
            taskReason = userConfig.RecDeleteRuleReqTaskReason;
            priority = userConfig.RecDeleteRuleTicketPriority;
            await CreateRuleDeleteTicket(deviceId, ruleUids, comment, deadline);
        }

        public async Task CreateUnusedRuleDeleteTicket(int deviceId, List<string> ruleUids, string comment = "", DateTime? deadline = null)
        {
            stateId = userConfig.RecDeleteRuleInitState;
            ticketTitle = userConfig.GetText("delete_unused_rules") + ": ";
            ticketReason = comment;
            taskTitle = userConfig.GetText("delete_unused_rule");
            taskReason = "";
            priority = userConfig.RecDeleteRuleTicketPriority;
            await CreateRuleDeleteTicket(deviceId, ruleUids, comment, deadline);
        }

        private async Task CreateRuleDeleteTicket(int deviceId, List<string> ruleUids, string comment = "", DateTime? deadline = null)
        {
            await wfHandler.Init();
            wfHandler.ActTicket = new WfTicket()
            {
                StateId = stateId,
                Title = ticketTitle + wfHandler.Devices.FirstOrDefault(x => x.Id == deviceId)?.Name ?? "",
                Requester = userConfig.User,
                Reason = ticketReason,
                Priority = priority,
                Deadline = deadline,
                Locked = true
            };
            foreach (var ruleUid in ruleUids)
            {
                wfHandler.ActReqTask = new WfReqTask()
                {
                    StateId = stateId,
                    Title = taskTitle + " " + ruleUid,
                    TaskType = WfTaskType.rule_delete.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    Reason = taskReason,
                    Locked = true
                };
                wfHandler.ActReqTask.Elements.Add(new WfReqElement()
                {
                    Field = ElemFieldType.rule.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    DeviceId = deviceId,
                    RuleUid = ruleUid
                });
                await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams() { StateId = wfHandler.MasterStateMatrix.LowestEndState }));
                wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            }
            wfHandler.AddTicketMode = true;
            long ticketId = await wfHandler.SaveTicket(wfHandler.ActTicket);
            if (ticketId > 0 && !string.IsNullOrWhiteSpace(comment))
            {
                await wfHandler.ConfAddCommentToTicket(comment);
            }
        }

        private async Task AddRequesterInfoToImplTask(long ticketId)
        {
            WfImplTask? implTask = await FindNewInterfaceImplTask(ticketId);
            if (implTask != null)
            {
                wfHandler.SetImplTaskEnv(implTask);
                string comment = $"{userConfig.GetText("requested_by")}: {userConfig.User.Name}";
                await wfHandler.ConfAddCommentToImplTask(comment);
            }
        }

        private async Task<WfImplTask?> FindNewInterfaceImplTask(long ticketId)
        {
            return (await FindNewInterfaceImplTasks(ticketId, null)).FirstOrDefault();
        }

        private async Task<List<WfImplTask>> FindNewInterfaceImplTasks(long ticketId, IEnumerable<long>? requestTaskIds)
        {
            WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
            if (ticket != null)
            {
                HashSet<long>? requestedTaskIds = requestTaskIds?.ToHashSet();
                List<WfImplTask> implTasks = [];
                foreach (WfReqTask reqTask in ticket.Tasks.Where(x => x.TaskType == WfTaskType.new_interface.ToString()))
                {
                    if (requestedTaskIds != null && !requestedTaskIds.Contains(reqTask.Id))
                    {
                        continue;
                    }
                    wfHandler.SetReqTaskEnv(reqTask);
                    WfImplTask? implTask = reqTask.ImplementationTasks.FirstOrDefault(x => x.ReqTaskId == reqTask.Id);
                    if (implTask != null)
                    {
                        implTasks.Add(implTask);
                    }
                    if (requestedTaskIds == null)
                    {
                        break;
                    }
                }
                return implTasks;
            }
            return [];
        }

        private static void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
        {
            if (exception == null)
            {
                if (ErrorFlag)
                {
                    Log.WriteWarning(title, message);
                }
                else
                {
                    Log.WriteInfo(title, message);
                }
            }
            else
            {
                Log.WriteError(title, message, exception);
            }
        }
    }
}
