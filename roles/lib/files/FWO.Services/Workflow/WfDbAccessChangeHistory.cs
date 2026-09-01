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
        /// Stores a structured workflow change and classifies selected UI content edits as audit-prove critical.
        /// </summary>
        private async Task LogWorkflowChange(long ticketId, ModellingTypes.ChangeType changeType, ChangeHistoryObjectType objectType,
            long objectId, string changeText, object? oldValue, object? newValue, UiUser? requester, bool contentChange)
        {
            if (WorkflowPhase is null)
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
                changeType = (int)changeType,
                objectType = (int)objectType,
                objectId,
                changeText,
                changer = UserConfig.User.Name,
                changeSource = "workflow",
                workflowPhase = (int)WorkflowPhase,
                oldData,
                newData,
                auditProveCritical = contentChange && IsUiContext && requester != null && requester.DbId != UserConfig.UserId
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
                task.TaskType,
                task.RequestAction,
                task.RuleAction,
                task.Tracking,
                task.Start,
                task.Stop,
                task.FreeText,
                task.Reason,
                task.AdditionalInfo,
                task.ManagementId,
                task.SelectedDevices,
                Owners = task.Owners.Select(owner => owner.Owner.Id),
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

        /// <summary>
        /// Selects standard workflow state and assignment fields for history entries.
        /// </summary>
        private static object WorkflowStateSnapshot(WfStatefulObject item)
        {
            return new
            {
                item.StateId,
                CurrentHandler = item.CurrentHandler?.DbId,
                RecentHandler = item.RecentHandler?.DbId,
                item.AssignedGroup
            };
        }

        /// <summary>
        /// Selects persisted approval fields for history entries.
        /// </summary>
        private static object ApprovalHistorySnapshot(WfApproval approval)
        {
            return new
            {
                approval.StateId,
                approval.ApprovalDate,
                approval.ApproverDn,
                approval.ApproverGroup,
                approval.AssignedGroup,
                approval.Deadline,
                approval.InitialApproval
            };
        }
    }
}
