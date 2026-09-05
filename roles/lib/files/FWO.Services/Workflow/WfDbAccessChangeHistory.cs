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
            // The snapshots travel as GraphQL variables and are serialized by the serializer the
            // connection was built with, which is Newtonsoft (ApiConstants.UseSystemTextJsonSerializer
            // is false). Json.NET cannot serialize a System.Text.Json.JsonElement - it would write the
            // wrapper's ValueKind instead of the snapshot - so the snapshots are passed on unchanged.
            // System.Text.Json is used in process only, to detect whether anything changed at all.
            if (JsonSerializer.Serialize(oldValue) == JsonSerializer.Serialize(newValue))
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
                changeSource = GlobalConst.kModuleWorkflow,
                workflowPhase = (int)WorkflowPhase,
                oldData = oldValue,
                newData = newValue,
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
        /// Selects the ticket fields written by the ticket state mutation.
        /// </summary>
        /// <remarks>
        /// A state snapshot has to cover exactly what its mutation persists: a field the mutation writes
        /// but the snapshot omits is both missing from the recorded change and invisible to the
        /// change detection in LogWorkflowChange, which would drop the entry altogether.
        /// </remarks>
        private static object TicketStateSnapshot(WfTicket ticket)
        {
            return new
            {
                ticket.StateId,
                ticket.CompletionDate,
                ticket.Deadline,
                ticket.Priority
            };
        }

        /// <summary>
        /// Selects the task fields written by the request and implementation task state mutations.
        /// </summary>
        /// <remarks>
        /// Both mutations persist the same set of fields, see the remark on TicketStateSnapshot.
        /// </remarks>
        private static object TaskStateSnapshot(WfTaskBase task)
        {
            return new
            {
                task.StateId,
                task.Start,
                task.Stop,
                CurrentHandler = task.CurrentHandler?.DbId,
                RecentHandler = task.RecentHandler?.DbId,
                task.AssignedGroup
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
