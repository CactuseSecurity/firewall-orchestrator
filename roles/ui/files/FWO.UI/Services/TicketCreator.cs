using System.Text.Json;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Middleware.Client;

namespace FWO.Ui.Services
{
    public class TicketCreator
    {
        private readonly RequestHandler reqHandler;
        private readonly UserConfig userConfig;
        private int stateId;
        private string ticketTitle = "";
        private string ticketReason = "";
        private string taskTitle = "";
        private string taskReason = "";
        private int priority;


        public TicketCreator(ApiConnection apiConnection, UserConfig userConfig, System.Security.Claims.ClaimsPrincipal authUser, MiddlewareClient middlewareClient, WorkflowPhases phase = WorkflowPhases.request)
        {
            reqHandler = new (LogMessage, userConfig, authUser, apiConnection, middlewareClient, phase);
            this.userConfig = userConfig;
        }

        public async Task<long> CreateRequestNewInterfaceTicket(FwoOwner owner, string reason = "")
        {
            await reqHandler.Init([owner.Id]);
            stateId = reqHandler.MasterStateMatrix.LowestEndState;
            reqHandler.SelectTicket(new RequestTicket()
                {
                    StateId = stateId,
                    Title = userConfig.ModReqTicketTitle,
                    Requester = userConfig.User,
                    Reason = reason
                },
                ObjAction.add);
            reqHandler.SelectReqTask(new RequestReqTask()
                {
                    StateId = stateId,
                    Title = userConfig.ModReqTaskTitle,
                    TaskType = TaskType.new_interface.ToString(),
                    Owners = [new() { Owner = owner }],
                    Reason = reason
                },
                ObjAction.add);
            await reqHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = reqHandler.MasterStateMatrix.LowestEndState}));
            reqHandler.ActTicket.Tasks.Add(reqHandler.ActReqTask);
            reqHandler.AddTicketMode = true;
            return await reqHandler.SaveTicket(reqHandler.ActTicket);
        }

        public async Task SetInterfaceId(long ticketId, long connId, FwoOwner owner)
        {
            await reqHandler.Init([owner.Id], true);
            RequestTicket? ticket = await reqHandler.ResolveTicket(ticketId);
            if(ticket != null)
            {
                RequestReqTask? reqTask = ticket.Tasks.FirstOrDefault(x => x.TaskType == TaskType.new_interface.ToString());
                if(reqTask != null)
                {
                    await reqHandler.AddAdditionalInfoToReqTask(reqTask, connId);
                }
            }
        }

        public async Task<bool> PromoteTicket(FwoOwner owner, long ticketId, string comment = "")
        {
            await reqHandler.Init([owner.Id]);
            RequestTicket? ticket = await reqHandler.ResolveTicket(ticketId);
            if(ticket != null)
            {
                reqHandler.SetTicketEnv(ticket);
                RequestReqTask? reqTask = ticket.Tasks.FirstOrDefault(x => x.TaskType == TaskType.new_interface.ToString());
                if(reqTask != null)
                {
                    reqHandler.SetReqTaskEnv(reqTask);
                    RequestImplTask? implTask = reqTask.ImplementationTasks.FirstOrDefault(x => x.ReqTaskId == reqTask.Id);
                    if(implTask != null)
                    {
                        await reqHandler.ContinueImplPhase(implTask);
                        if(comment != "")
                        {
                            await reqHandler.ConfAddCommentToImplTask(comment);
                        }
                        int newState = reqHandler.MasterStateMatrix.LowestEndState;
                        await reqHandler.PromoteImplTask(new(){ StateId = newState });
                        return true;
                    }
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
            await reqHandler.Init([]);
            reqHandler.ActTicket = new RequestTicket()
            {
                StateId = stateId,
                Title = ticketTitle + reqHandler.Devices.FirstOrDefault(x => x.Id == deviceId)?.Name ?? "",
                Requester = userConfig.User,
                Reason = ticketReason,
                Priority = priority,
                Deadline = deadline
            };
            foreach(var ruleUid in ruleUids)
            {
                reqHandler.ActReqTask = new RequestReqTask()
                {
                    StateId = stateId,
                    Title = taskTitle + " " + ruleUid,
                    TaskType = TaskType.rule_delete.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    Reason = taskReason
                };
                reqHandler.ActReqTask.Elements.Add(new RequestReqElement()
                {
                    Field = ElemFieldType.rule.ToString(),
                    RequestAction = RequestAction.delete.ToString(),
                    DeviceId = deviceId,
                    RuleUid = ruleUid
                });
                await reqHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = reqHandler.MasterStateMatrix.LowestEndState}));
                reqHandler.ActTicket.Tasks.Add(reqHandler.ActReqTask);
            }
            reqHandler.AddTicketMode = true;
            await reqHandler.SaveTicket(reqHandler.ActTicket);
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
