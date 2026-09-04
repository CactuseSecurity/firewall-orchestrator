using System.Text.Json;
using FWO.Basics;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        /// <summary>
        /// Identifies the workflow object a change history entry belongs to.
        /// </summary>
        /// <param name="TicketId">Id of the ticket owning the changed object.</param>
        /// <param name="ChangeType">Kind of change applied to the object.</param>
        /// <param name="ObjectType">Type of the changed workflow object.</param>
        /// <param name="ObjectId">Id of the changed workflow object.</param>
        private sealed record WorkflowChangeTarget(long TicketId, ModellingTypes.ChangeType ChangeType,
            ChangeHistoryObjectType ObjectType, long ObjectId);

        /// <summary>
        /// Loads the stored state of a ticket before a change, so it can be compared against the new state.
        /// </summary>
        /// <param name="ticketId">Id of the ticket to load.</param>
        /// <returns>The stored ticket, or null when it could not be loaded.</returns>
        /// <remarks>
        /// GetTicket reports its own errors and yields an empty ticket on failure. Returning null in that
        /// case keeps a failed read from being written to the history as an empty previous state.
        /// </remarks>
        public async Task<WfTicket?> LoadPreviousTicket(long ticketId)
        {
            WfTicket previousTicket = await GetTicket(ticketId);
            return previousTicket.Id == ticketId ? previousTicket : null;
        }

        /// <summary>
        /// Stores a structured workflow change and classifies selected UI content edits as audit-proof critical.
        /// </summary>
        /// <remarks>
        /// Never throws: change history is observational, so a failure to record a change must not abort the
        /// workflow operation that caused it, nor the state change actions that follow it.
        /// </remarks>
        private async Task LogWorkflowChange(WorkflowChangeTarget target, string changeText,
            object? oldValue, object? newValue, UiUser? requester, bool contentChange)
        {
            JsonElement oldData = JsonSerializer.SerializeToElement(oldValue);
            JsonElement newData = JsonSerializer.SerializeToElement(newValue);
            if (oldData.GetRawText() == newData.GetRawText())
            {
                return;
            }

            var variables = new
            {
                appId = (int?)null,
                module = GlobalConst.kModuleWorkflow,
                ticketId = target.TicketId,
                changeType = (int)target.ChangeType,
                objectType = (int)target.ObjectType,
                objectId = target.ObjectId,
                changeText,
                changer = UserConfig.User.Name,
                changeSource = GlobalConst.kWorkflow,
                workflowPhase = (int)WorkflowPhase,
                oldData,
                newData,
                auditProofCritical = contentChange && IsUiContext && requester != null && requester.DbId != UserConfig.UserId
            };
            try
            {
                await ApiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addHistoryEntry, variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("Workflow Change History",
                    $"Could not record {target.ChangeType} of {target.ObjectType} {target.ObjectId} for ticket {target.TicketId}.", exception);
            }
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
