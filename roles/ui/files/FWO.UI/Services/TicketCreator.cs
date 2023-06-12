using System.Text.Json;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Logging;

namespace FWO.Ui.Services
{
    public class TicketCreator
    {
        private RequestHandler reqHandler;
        private UserConfig userConfig;


        public TicketCreator(ApiConnection apiConnection, UserConfig userConfig)
        {
            reqHandler = new RequestHandler(LogMessage, userConfig, apiConnection, WorkflowPhases.request);
            this.userConfig = userConfig;
        }

        public async Task CreateRuleDeleteTicket(int deviceId, List<string> ruleUids, string comment = "", DateTime? deadline = null)
        {
            if(userConfig.RecAutoCreateDeleteTicket)
            {
                await reqHandler.Init();
                reqHandler.ActTicket = new RequestTicket()
                {
                    StateId = userConfig.RecDeleteRuleInitState,
                    Title = userConfig.RecDeleteRuleTicketTitle + " " + reqHandler.Devices.FirstOrDefault(x => x.Id == deviceId)?.Name ?? "",
                    Requester = userConfig.User, // role? recertifier = requester?
                    Reason = userConfig.RecDeleteRuleTicketReason + " " + comment,
                    Priority = userConfig.RecDeleteRuleTicketPriority,
                    Deadline = deadline
                };
                foreach(var ruleUid in ruleUids)
                {
                    reqHandler.ActReqTask = new RequestReqTask()
                    {
                        StateId = userConfig.RecDeleteRuleInitState,
                        Title = userConfig.RecDeleteRuleReqTaskTitle + " " + ruleUid,
                        TaskType = TaskType.rule_delete.ToString(),
                        RequestAction = RequestAction.delete.ToString(),
                        Reason = userConfig.RecDeleteRuleReqTaskReason
                    };
                    reqHandler.ActReqTask.Elements.Add(new RequestReqElement()
                    {
                        Field = ElemFieldType.rule.ToString(),
                        RequestAction = RequestAction.delete.ToString(),
                        DeviceId = deviceId,
                        RuleUid = ruleUid
                    });
                    await reqHandler.AddApproval(JsonSerializer.Serialize(new ApprovalParams(){StateId = userConfig.RecDeleteRuleInitState}));
                    reqHandler.ActTicket.Tasks.Add(reqHandler.ActReqTask);
                }
                reqHandler.AddTicketMode = true;
                await reqHandler.SaveTicket(reqHandler.ActTicket);
            }
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
