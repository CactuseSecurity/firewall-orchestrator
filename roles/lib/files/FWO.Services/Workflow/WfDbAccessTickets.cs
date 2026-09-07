using System.Collections.Generic;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Linq;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        /// <summary>
        /// Persists a newly created ticket and triggers the initial workflow actions.
        /// </summary>
        public async Task<WfTicket> AddTicketToDb(WfTicket ticket)
        {
            try
            {
                // Callers may supply either plain IP strings or parsed CIDRs. Deriving the CIDRs first makes sure
                // that IP strings coming e.g. from the REST API survive the following normalization step.
                ticket.UpdateCidrsInTaskElements();
                ticket.UpdateIpStringsFromCidrInTaskElements();
                var variables = BuildTicketVariables(ticket);
                variables["requesterId"] = ticket.Requester?.DbId;
                variables["requestTasks"] = new WfTicketWriter(ticket);
                variables["locked"] = ticket.Locked;
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newTicket, variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8001"), true);
                    return ticket;
                }

                int newStateId = ticket.StateId;
                ticket = await GetTicket(returnIds[0].NewIdLong);
                ticket.MarkCreatedStateChanged(newStateId);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
                return ticket;
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
            }

            return ticket;
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
