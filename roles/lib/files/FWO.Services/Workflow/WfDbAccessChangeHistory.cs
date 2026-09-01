using System.Text.Json;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        /// <summary>
        /// Stores a structured workflow change made after the request phase by someone other than the requester.
        /// </summary>
        private async Task LogWorkflowChange(long ticketId, ChangeHistoryObjectType objectType, long objectId,
            string changeText, object oldValue, object newValue, UiUser? requester)
        {
            if (WorkflowPhase is null || WorkflowPhase == WorkflowPhases.request || requester?.DbId == UserConfig.UserId)
            {
                return;
            }

            JsonElement oldData = JsonSerializer.SerializeToElement(oldValue);
            JsonElement newData = JsonSerializer.SerializeToElement(newValue);
            if (oldData.GetRawText() == newData.GetRawText())
            {
                return;
            }

            var variables = new
            {
                appId = (int?)null,
                ticketId,
                changeType = (int)ModellingTypes.ChangeType.Update,
                objectType = (int)objectType,
                objectId,
                changeText,
                changer = UserConfig.User.Name,
                changeSource = "workflow",
                workflowPhase = (int)WorkflowPhase,
                oldData,
                newData
            };
            await ApiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addHistoryEntry, variables);
        }

        /// <summary>
        /// Selects the persisted ticket fields included in change history.
        /// </summary>
        private static object TicketHistorySnapshot(WfTicket ticket)
        {
            return new
            {
                ticket.Title,
                ticket.StateId,
                ticket.Reason,
                ticket.Deadline,
                ticket.Priority
            };
        }

        /// <summary>
        /// Selects the persisted request-task fields included in change history.
        /// </summary>
        private static object RequestTaskHistorySnapshot(WfReqTask task)
        {
            return new
            {
                task.Title,
                task.StateId,
                task.TaskType,
                task.RequestAction,
                task.RuleAction,
                task.Tracking,
                task.Start,
                task.Stop,
                task.FreeText,
                task.SelectedDevices,
                Elements = task.Elements.Select(element => new
                {
                    element.Id,
                    element.Field,
                    element.RequestAction,
                    element.IpString,
                    element.IpEnd,
                    element.Port,
                    element.PortEnd,
                    element.ProtoId,
                    element.NetworkId,
                    element.ServiceId,
                    element.Name,
                    element.GroupName
                })
            };
        }

        /// <summary>
        /// Selects the persisted implementation-task fields included in change history.
        /// </summary>
        private static object ImplementationTaskHistorySnapshot(WfImplTask task)
        {
            return new
            {
                task.Title,
                task.StateId,
                task.TaskType,
                task.ImplAction,
                task.RuleAction,
                task.Tracking,
                task.Start,
                task.Stop,
                task.FreeText,
                task.DeviceId,
                Elements = task.ImplElements.Select(element => new
                {
                    element.Id,
                    element.Field,
                    element.ImplAction,
                    element.IpString,
                    element.IpEnd,
                    element.Port,
                    element.PortEnd,
                    element.ProtoId,
                    element.NetworkId,
                    element.ServiceId,
                    element.Name,
                    element.GroupName
                })
            };
        }
    }
}
