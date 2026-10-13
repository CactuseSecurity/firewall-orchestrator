using System.Collections.Generic;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Api.Client.Queries;
using System.Linq;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        private const string kInitialActionsFailedText = "Initial workflow actions failed";

        /// <summary>
        /// Persists a newly created ticket and triggers the initial workflow actions.
        /// </summary>
        /// <returns>
        /// The ticket (with id 0 only if it was not saved) and whether its initial workflow actions failed. A failure
        /// of the actions does not undo the saved ticket; it is shown, logged, raised as alert and recorded in the
        /// ticket's change history, and returned so that callers do not report the creation as a plain success.
        /// A ticket that was saved but could not be read back keeps its new id and is reported with failed initial
        /// actions (which are not executed then), so that a caller does not create it a second time.
        /// </returns>
        public async Task<WfTicketCreationResult> AddTicketToDb(WfTicket ticket)
        {
            long newTicketId = 0;
            try
            {
                // Callers may supply either plain IP strings or parsed CIDRs. Deriving the CIDRs first makes sure
                // that IP strings coming e.g. from the REST API survive the following normalization step.
                ticket.UpdateCidrsInTaskElements();
                ticket.UpdateIpStringsFromCidrInTaskElements();
                var variables = BuildTicketVariables(ticket);
                variables["preWorkflowTicketReference"] = ticket.PreWorkflowTicketReference;
                variables["requesterId"] = ticket.Requester?.DbId;
                variables["requestTasks"] = new WfTicketWriter(ticket);
                variables["locked"] = ticket.Locked;
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newTicket, variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8001"), true);
                    return new WfTicketCreationResult(ticket, false);
                }

                newTicketId = returnIds[0].NewIdLong;
                int newStateId = ticket.StateId;
                WfTicket savedTicket = await GetTicket(newTicketId);
                if (savedTicket.Id != newTicketId)
                {
                    return await SavedWithoutInitialActions(ticket, newTicketId, null);
                }
                ticket = savedTicket;
                await LogCreatedRequestTasks(ticket);
                ticket.MarkCreatedStateChanged(newStateId);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
                if (newTicketId <= 0)
                {
                    return new WfTicketCreationResult(ticket, false);
                }
                if (ticket.Id == newTicketId)
                {
                    // read back completely, but failed while preparing the initial actions
                    Log.WriteError("Create Request", $"Preparing the initial workflow actions of ticket {newTicketId} failed.", exception);
                    await RecordInitialActionsFailure(ticket, exception);
                    return new WfTicketCreationResult(ticket, true);
                }
                return await SavedWithoutInitialActions(ticket, newTicketId, exception);
            }

            try
            {
                await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, GetRequesterDn(ticket));
                await DoCreatedRequestTaskActions(ticket);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
                Log.WriteError("Create Request", "Workflow actions failed while creating the request ticket.", exception);
                await RecordInitialActionsFailure(ticket, exception);
                return new WfTicketCreationResult(ticket, true);
            }

            return new WfTicketCreationResult(ticket, false);
        }

        /// <summary>
        /// Reports a ticket that was saved but could not be read back: it keeps its new id, so that callers report it
        /// as saved instead of creating it again, and its initial workflow actions are not executed on the incomplete
        /// in-memory copy but recorded as failed.
        /// </summary>
        private async Task<WfTicketCreationResult> SavedWithoutInitialActions(WfTicket ticket, long newTicketId, Exception? cause)
        {
            ticket.Id = newTicketId;
            InvalidOperationException failure = new(
                $"Ticket {newTicketId} was saved but could not be read back, so its initial workflow actions were not executed.", cause);
            Log.WriteError("Create Request", failure.Message, cause);
            await RecordInitialActionsFailure(ticket, failure);
            return new WfTicketCreationResult(ticket, true, true);
        }

        /// <summary>
        /// Makes a failure of the initial workflow actions visible beyond the log: as an alert for the administrators
        /// and as an entry in the ticket's change history, since the saved ticket may imply actions that did not run.
        /// </summary>
        private async Task RecordInitialActionsFailure(WfTicket ticket, Exception exception)
        {
            await LogWorkflowChange(new(ticket.Id, ModellingTypes.ChangeType.Update, ChangeHistoryObjectType.Ticket, ticket.Id),
                kInitialActionsFailedText, null, new { initialActionsStatus = WfTicketCreationResult.kActionsFailed, error = exception.Message },
                ticket.Requester, false);
            await AlertHelper.SetAlert(ApiConnection, UserConfig.GetText("save_request"),
                $"Ticket {ticket.Id} was saved, but its initial workflow actions failed: {exception.Message}",
                GlobalConst.kWorkflow, AlertCode.WorkflowAlert,
                new AlertHelper.AdditionalAlertData { JsonData = new { ticketId = ticket.Id }, CompareDesc = true });
        }

        /// <summary>
        /// Records an insert history entry for each request task created together with the ticket.
        /// </summary>
        /// <remarks>
        /// The tasks are inserted by the nested ticket mutation, so AddReqTaskToDb and its logging are bypassed.
        /// The ticket is new, hence there is no previous state and its requester is taken from the reloaded ticket.
        /// </remarks>
        private async Task LogCreatedRequestTasks(WfTicket ticket)
        {
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                await LogWorkflowChange(new(ticket.Id, ModellingTypes.ChangeType.Insert, ChangeHistoryObjectType.RequestTask, reqTask.Id),
                    "Added workflow request task", null, RequestTaskHistorySnapshot(reqTask), ticket.Requester, true);
            }
        }

        /// <summary>
        /// Triggers the initial workflow actions for request tasks created with the ticket.
        /// </summary>
        private async Task DoCreatedRequestTaskActions(WfTicket ticket)
        {
            // SyncActTicketFromReqTask writes back into ticket.Tasks, so iterate over a snapshot.
            List<WfReqTask> createdTasks = new(ticket.Tasks);
            foreach (WfReqTask reqTask in createdTasks)
            {
                int newStateId = reqTask.StateId;
                reqTask.MarkCreatedStateChanged(newStateId);
                await ActionHandler.DoStateChangeActions(reqTask, WfObjectScopes.RequestTask, reqTask.Owners.Count > 0 ? reqTask.Owners.First().Owner : null, reqTask.TicketId);
            }
        }

        /// <summary>
        /// Builds the base ticket variables shared by insert and update operations.
        /// </summary>
        private static Dictionary<string, object?> BuildTicketVariables(WfTicket ticket)
        {
            return new Dictionary<string, object?>
            {
                ["title"] = ticket.Title,
                ["state"] = ticket.StateId,
                ["reason"] = ticket.Reason,
                ["deadline"] = ticket.Deadline,
                ["priority"] = ticket.Priority
            };
        }

        /// <summary>
        /// Updates an existing ticket and runs ticket-level state actions when the update succeeds.
        /// </summary>
        public async Task<WfTicket> UpdateTicketInDb(WfTicket ticket)
        {
            WfTicket? previousTicket = await LoadPreviousTicket(ticket.Id);
            try
            {
                // Ticket locking is task-scoped: header metadata remains writable while request-task updates are guarded separately.
                var variables = BuildTicketVariables(ticket);
                variables["id"] = ticket.Id;
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateTicket, variables)).UpdatedIdLong;
                if (udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    if (previousTicket != null)
                    {
                        await LogWorkflowChange(new(ticket.Id, ModellingTypes.ChangeType.Update, ChangeHistoryObjectType.Ticket, ticket.Id),
                            "Updated workflow ticket", TicketHistorySnapshot(previousTicket), TicketHistorySnapshot(ticket), previousTicket.Requester, true);
                    }
                    await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, GetRequesterDn(ticket));
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }
    }
}
