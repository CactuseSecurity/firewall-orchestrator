using System.Text.Json;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Middleware.Client;

namespace FWO.Services
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


        public TicketCreator(ApiConnection apiConnection, UserConfig userConfig, System.Security.Claims.ClaimsPrincipal authUser, MiddlewareClient middlewareClient, WorkflowPhases phase = WorkflowPhases.request)
        {
            wfHandler = new (LogMessage, userConfig, authUser, apiConnection, middlewareClient, phase);
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
        }

        public async Task<WfTicket> CreateTicket(FwoOwner owner, List<WfReqTask> reqTasks, string title, int? stateId, string reason = "")
        {
            await wfHandler.Init([owner.Id]);
            stateId ??= wfHandler.MasterStateMatrix.LowestEndState;
            await wfHandler.SelectTicket(new WfTicket()
                {
                    StateId = (int)stateId,
                    Title = title,
                    Requester = userConfig.User,
                    Reason = reason
                },
                ObjAction.add);
            foreach(var reqTask in reqTasks)
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
                        AdditionalInfo = reqTask.AdditionalInfo
                    },
                    ObjAction.add);
                await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = (int)stateId}));
                wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            }
            wfHandler.AddTicketMode = true;
            wfHandler.ActTicket.UpdateCidrsInTaskElements();
            await wfHandler.SaveTicket(wfHandler.ActTicket);
            foreach(var reqtask in reqTasks.Where(t => t.Comments.Count > 0))
            {
                WfReqTask? reqTaskToChange = wfHandler.ActTicket.Tasks.FirstOrDefault(x => x.TaskType == reqtask.TaskType &&
                    x.ManagementId == reqtask.ManagementId && x.Title == reqtask.Title && x.TaskNumber == reqtask.TaskNumber);
                if(reqTaskToChange != null)
                {
                    wfHandler.SetReqTaskEnv(reqTaskToChange);
                    await wfHandler.ConfAddCommentToReqTask(reqtask.Comments.First().Comment.CommentText);
                }
            }
            return wfHandler.ActTicket;
        }

        public async Task<long> CreateRequestNewInterfaceTicket(FwoOwner owner, FwoOwner requestingOwner, string reason = "")
        {
            await wfHandler.Init([owner.Id]);
            stateId = wfHandler.MasterStateMatrix.LowestEndState;
            await wfHandler.SelectTicket(new WfTicket()
                {
                    StateId = stateId,
                    Title = userConfig.ModReqTicketTitle,
                    Requester = userConfig.User,
                    Reason = reason
                },
                ObjAction.add);
            Dictionary<string, string>? addInfo = new() { {AdditionalInfoKeys.ReqOwner, requestingOwner.Id.ToString()} };
            wfHandler.SelectReqTask(new WfReqTask()
                {
                    StateId = stateId,
                    Title = userConfig.ModReqTaskTitle,
                    TaskType = WfTaskType.new_interface.ToString(),
                    Owners = [new() { Owner = owner }],
                    Reason = reason,
                    AdditionalInfo = JsonSerializer.Serialize(addInfo)
                },
                ObjAction.add);
            await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = wfHandler.MasterStateMatrix.LowestEndState}));
            wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            wfHandler.AddTicketMode = true;
            long ticketId = await wfHandler.SaveTicket(wfHandler.ActTicket);
            if(ticketId > 0)
            {
                await AddRequesterInfoToImplTask(ticketId, requestingOwner);
            }
            return ticketId;
        }

        public async Task SetInterfaceId(long ticketId, long connId, FwoOwner owner)
        {
            await wfHandler.Init([owner.Id], true);
            WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
            if(ticket != null)
            {
                WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(x => x.TaskType == WfTaskType.new_interface.ToString());
                if(reqTask != null)
                {
                    await wfHandler.SetAddInfoInReqTask(reqTask, AdditionalInfoKeys.ConnId, connId.ToString());
                }
            }
        }

        public async Task<bool> PromoteNewInterfaceImplTask(FwoOwner owner, long ticketId, ExtStates extState, string comment = "")
        {
            ExtStateHandler extStateHandler = new(apiConnection);
            await wfHandler.Init([owner.Id]);
            WfImplTask? implTask = await FindNewInterfaceImplTask(ticketId);
            if(implTask != null)
            {
                await wfHandler.ContinueImplPhase(implTask);
                if(comment != "")
                {
                    await wfHandler.ConfAddCommentToImplTask(comment);
                }
                int? newState = extStateHandler.GetInternalStateId(extState);
                if(newState != null)
                {
                    await wfHandler.PromoteImplTask(new(){ StateId = (int)newState });
                    return true;
                } 
            }
            return false;
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
            await wfHandler.Init([]);
            wfHandler.ActTicket = new WfTicket()
            {
                StateId = stateId,
                Title = ticketTitle + wfHandler.Devices.FirstOrDefault(x => x.Id == deviceId)?.Name ?? "",
                Requester = userConfig.User,
                Reason = ticketReason,
                Priority = priority,
                Deadline = deadline
            };
            foreach(var ruleUid in ruleUids)
            {
                wfHandler.ActReqTask = new WfReqTask()
                {
                    StateId = stateId,
                    Title = taskTitle + " " + ruleUid,
                    TaskType = WfTaskType.rule_delete.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    Reason = taskReason
                };
                wfHandler.ActReqTask.Elements.Add(new WfReqElement()
                {
                    Field = ElemFieldType.rule.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    DeviceId = deviceId,
                    RuleUid = ruleUid
                });
                await wfHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = wfHandler.MasterStateMatrix.LowestEndState}));
                wfHandler.ActTicket.Tasks.Add(wfHandler.ActReqTask);
            }
            wfHandler.AddTicketMode = true;
            await wfHandler.SaveTicket(wfHandler.ActTicket);
        }

        private async Task AddRequesterInfoToImplTask(long ticketId, FwoOwner owner)
        {
            WfImplTask? implTask = await FindNewInterfaceImplTask(ticketId);
            if(implTask != null)
            {
                wfHandler.SetImplTaskEnv(implTask);
                string comment = $"{userConfig.GetText("requested_by")}: {userConfig.User.Name}"; // {userConfig.GetText("for")} {owner.Display()}";
                await wfHandler.ConfAddCommentToImplTask(comment);
            }
        }

        private async Task<WfImplTask?> FindNewInterfaceImplTask(long ticketId)
        {
            WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
            if(ticket != null)
            {
                WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(x => x.TaskType == WfTaskType.new_interface.ToString());
                if(reqTask != null)
                {
                    wfHandler.SetReqTaskEnv(reqTask);
                    return reqTask.ImplementationTasks.FirstOrDefault(x => x.ReqTaskId == reqTask.Id);
                }
            }
            return null;
        }
        
        private void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
        {
            if (exception == null)
            {
                if(ErrorFlag)
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
