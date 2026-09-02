using FWO.Data;
using FWO.Data.Workflow;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Collections.Generic;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess(Action<Exception?, string, string, bool> DisplayMessageInUi, UserConfig UserConfig, ApiConnection ApiConnection, ActionHandler ActionHandler, bool AsAdmin)
    {
        public async Task<List<WfTicket>> FetchTickets(StateMatrix stateMatrix, List<int>? ownerIds = null, bool allStates = false, bool fullTickets = false,
            Func<WfTicket, bool>? ticketFilter = null)
        {
            List<WfTicket> tickets = [];
            try
            {
                int fromState = allStates ? 0 : stateMatrix.LowestInputState;
                int toState = allStates ? 999 : stateMatrix.LowestEndState;

                tickets = await ApiConnection.SendQueryAsync<List<WfTicket>>(
                    fullTickets ? RequestQueries.getFullTickets : RequestQueries.getTickets,
                    new { fromState, toState });
                if (UserConfig.ReqOwnerBased && !AsAdmin)
                {
                    tickets = await FilterWrongOwnersOut(tickets, ownerIds);
                }
                tickets = ApplyTicketFilter(tickets, ticketFilter);
                FinalizeTickets(tickets, fullTickets);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return tickets;
        }

        public async Task<WfTicket?> FetchTicket(long ticketId, List<int>? ownerIds = null, Func<WfTicket, bool>? ticketFilter = null)
        {
            WfTicket? ticket = null;
            try
            {
                ticket = await GetTicket(ticketId);
                if (UserConfig.ReqOwnerBased && !AsAdmin)
                {
                    ticket = (await FilterWrongOwnersOut([ticket], ownerIds)).FirstOrDefault();
                }
                if (ticket != null && ticketFilter != null && !ticketFilter(ticket))
                {
                    ticket = null;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return ticket;
        }

        private async Task<List<WfTicket>> FilterWrongOwnersOut(List<WfTicket> ticketsIn, List<int>? ownerIds)
        {
            if (ownerIds == null || ownerIds.Count == 0)
            {
                return [];
            }
            List<long> registeredTickets = (await ApiConnection.SendQueryAsync<List<TicketId>>(RequestQueries.getOwnerTicketIds, new { ownerIds })).ConvertAll(t => t.Id);
            foreach (var ticket in ticketsIn.Where(ti => !ti.IsEditableForOwner(registeredTickets, ownerIds, UserConfig.UserId)))
            {
                ticket.Editable = false;
            }
            return [.. ticketsIn.Where(ti => ti.IsVisibleForOwner(registeredTickets, ownerIds, UserConfig.UserId))];
        }

        public async Task<WfTicket> GetTicket(long id)
        {
            WfTicket ticket = new();
            try
            {
                var Variables = new { id };
                ticket = await ApiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, Variables);
                ticket.UpdateCidrsInTaskElements();
                ticket.ResetStateChangeTracking();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return ticket;
        }

        /// <summary>
        /// Loads tickets filtered by task type, state range and creation window.
        /// </summary>
        /// <param name="taskType">Workflow task type to query.</param>
        /// <param name="startState">Lower state bound.</param>
        /// <param name="endState">Upper state bound.</param>
        /// <param name="createdFrom">Inclusive lower creation timestamp bound.</param>
        /// <param name="createdUntil">Inclusive upper creation timestamp bound.</param>
        /// <param name="ticketFilter">Optional in-memory filter applied after loading.</param>
        /// <returns>Matching tickets.</returns>
        public async Task<List<WfTicket>> GetTicketsByParameters(string taskType, int startState, int endState, DateTime? createdFrom, DateTime? createdUntil,
            Func<WfTicket, bool>? ticketFilter = null)
        {
            List<WfTicket> tickets = [];
            try
            {
                var Variables = new
                {
                    createdFrom = createdFrom,
                    createdUntil = createdUntil,
                    taskType = taskType,
                    fromState = startState,
                    toState = endState
                };
                tickets = await ApiConnection.SendQueryAsync<List<WfTicket>>(RequestQueries.getTicketsByParameters, Variables);
                tickets = ApplyTicketFilter(tickets, ticketFilter);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return tickets;
        }

        private static List<WfTicket> ApplyTicketFilter(List<WfTicket> tickets, Func<WfTicket, bool>? ticketFilter)
        {
            if (ticketFilter == null)
            {
                return tickets;
            }

            List<WfTicket> filteredTickets = [];
            foreach (WfTicket ticket in tickets)
            {
                if (ticketFilter(ticket))
                {
                    filteredTickets.Add(ticket);
                }
            }
            return filteredTickets;
        }

        private static void FinalizeTickets(List<WfTicket> tickets, bool fullTickets)
        {
            foreach (WfTicket ticket in tickets)
            {
                if (fullTickets)
                {
                    ticket.UpdateCidrsInTaskElements();
                }
                ticket.ResetStateChangeTracking();
            }
        }
        // Approvals

        public async Task<long> AddApprovalToDb(WfApproval approval)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    taskId = approval.TaskId,
                    state = approval.StateId,
                    approverGroup = approval.ApproverGroup,
                    tenant = approval.TenantId,
                    deadline = approval.Deadline,
                    initialApproval = approval.InitialApproval
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newApproval, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_approval"), UserConfig.GetText("E8009"), true);
                }
                else
                {
                    int newStateId = approval.StateId;
                    returnId = returnIds[0].NewIdLong;
                    approval.Id = returnId;
                    approval.MarkCreatedStateChanged(newStateId);
                    await ActionHandler.DoStateChangeActions(approval, WfObjectScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_approval"), "", true);
            }
            return returnId;
        }

        public async Task UpdateApprovalInDb(WfApproval approval, bool triggerActions = true)
        {
            try
            {
                var Variables = new
                {
                    id = approval.Id,
                    state = approval.StateId,
                    approvalDate = approval.ApprovalDate,
                    approver = approval.ApproverDn,  // todo: Dn or uiuser??
                    assignedGroup = approval.AssignedGroup
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateApproval, Variables)).UpdatedIdLong;
                if (udId != approval.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_approval"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    if (triggerActions)
                    {
                        await ActionHandler.DoStateChangeActions(approval, WfObjectScopes.Approval);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_approval"), "", true);
            }
        }


        private static Dictionary<string, object?> BuildElementBaseVariables(WfElementBase element, Cidr? cidr, Cidr? cidrEnd)
        {
            bool hasServiceReference = HasId(element.ServiceId) || HasId(element.FlowServiceObjectId) || HasId(element.FlowServiceGroupId);
            return new Dictionary<string, object?>
            {
                ["ip"] = cidr != null && cidr.Valid ? cidr.CidrString : null,
                ["ipEnd"] = cidrEnd != null && cidrEnd.Valid ? cidrEnd.CidrString : null,
                ["port"] = hasServiceReference && !HasValidPort(element.Port) ? null : element.Port,
                ["portEnd"] = hasServiceReference && !HasValidPort(element.PortEnd) ? null : element.PortEnd,
                ["proto"] = hasServiceReference && !HasId(element.ProtoId) ? null : element.ProtoId,
                ["networkObjId"] = element.NetworkId,
                ["serviceId"] = element.ServiceId,
                ["field"] = element.Field,
                ["userId"] = element.UserId,
                ["originalNatId"] = element.OriginalNatId,
                ["ruleUid"] = element.RuleUid,
                ["groupName"] = element.GroupName,
                ["name"] = element.Name
            };
        }

        private static bool HasId(long? id)
        {
            return id.HasValue && id.Value != 0;
        }

        private static bool HasId(int? id)
        {
            return id.HasValue && id.Value != 0;
        }

        private static bool HasValidPort(int? port)
        {
            return port.HasValue && port.Value > 0;
        }


        // Comments

        public async Task<long> AddCommentToDb(WfComment comment)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    refId = comment.RefId,
                    scope = comment.Scope,
                    creationDate = comment.CreationDate,
                    creator = comment.Creator.DbId,
                    text = comment.CommentText
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newComment, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_comment"), UserConfig.GetText("E8012"), true);
                }
                else
                {
                    returnId = returnIds[0].NewIdLong;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_comment"), "", true);
            }
            return returnId;
        }

        public async Task AssignCommentToTicketInDb(long ticketId, long commentId)
        {
            await AssignCommentToDb(RequestQueries.addCommentToTicket, new { ticketId, commentId });
        }

        public async Task AssignCommentToReqTaskInDb(long taskId, long commentId)
        {
            await AssignCommentToDb(RequestQueries.addCommentToReqTask, new { taskId, commentId });
        }

        public async Task AssignCommentToImplTaskInDb(long taskId, long commentId)
        {
            await AssignCommentToDb(RequestQueries.addCommentToImplTask, new { taskId, commentId });
        }

        public async Task AssignCommentToApprovalInDb(long approvalId, long commentId)
        {
            await AssignCommentToDb(RequestQueries.addCommentToApproval, new { approvalId, commentId });
        }

        private async Task AssignCommentToDb(string query, object variables)
        {
            try
            {
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(query, variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_comment"), UserConfig.GetText("E8012"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_comment"), "", true);
            }
        }


        // Owners

        public async Task UpdateOwnersInDb(WfReqTask reqtask)
        {
            try
            {
                foreach (var owner in reqtask.RemovedOwners)
                {
                    await RemoveOwnerInDb(reqtask.Id, owner.Id);
                    FwoOwnerDataHelper? oldOwner = reqtask.Owners.FirstOrDefault(x => x.Owner.Id == owner.Id);
                    if (oldOwner != null)
                    {
                        reqtask.Owners.Remove(oldOwner);
                    }
                }
                reqtask.RemovedOwners = [];

                foreach (var owner in reqtask.NewOwners)
                {
                    await AssignOwnerInDb(reqtask.Id, owner.Id);
                    reqtask.Owners.Add(new() { Owner = owner });
                    await ActionHandler.DoOwnerChangeActions(reqtask, owner, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        private async Task AssignOwnerInDb(long reqTaskId, long ownerId)
        {
            try
            {
                var Variables = new { reqTaskId, ownerId };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.addOwnerToReqTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("assign_owner"), UserConfig.GetText("E8015"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("assign_owner"), "", true);
            }
        }

        private async Task RemoveOwnerInDb(long reqTaskId, long ownerId)
        {
            try
            {
                var Variables = new { reqTaskId, ownerId };
                if ((await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.removeOwnerFromReqTask, Variables)).AffectedRows == 0)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("assign_owner"), UserConfig.GetText("E8016"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("assign_owner"), "", true);
            }
        }


        // State changes

        public async Task UpdateTicketStateInDb(WfTicket ticket, bool triggerActions = true)
        {
            try
            {
                var Variables = new
                {
                    id = ticket.Id,
                    state = ticket.StateId,
                    closed = ticket.CompletionDate,
                    deadline = ticket.Deadline,
                    priority = ticket.Priority
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateTicketState, Variables)).UpdatedIdLong;
                if (udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    if (triggerActions)
                    {
                        await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, GetRequesterDn(ticket));
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
        }

        public async Task UpdateReqTaskStateInDb(WfReqTask reqtask, bool triggerActions = true)
        {
            try
            {
                var Variables = new
                {
                    id = reqtask.Id,
                    state = reqtask.StateId,
                    start = reqtask.Start,
                    stop = reqtask.Stop,
                    handler = reqtask.CurrentHandler?.DbId,
                    recentHandler = reqtask.RecentHandler?.DbId,
                    assignedGroup = reqtask.AssignedGroup
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskState, Variables)).UpdatedIdLong;
                if (udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    if (triggerActions)
                    {
                        await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        private static string? GetRequesterDn(WfTicket ticket)
        {
            return !string.IsNullOrWhiteSpace(ticket.Requester?.Dn) ? ticket.Requester.Dn : ticket.RequesterDn;
        }

        public async Task UpdateImplTaskStateInDb(WfImplTask impltask, bool triggerActions = true)
        {
            try
            {
                var Variables = new
                {
                    id = impltask.Id,
                    state = impltask.StateId,
                    start = impltask.Start,
                    stop = impltask.Stop,
                    handler = impltask.CurrentHandler?.DbId,
                    recentHandler = impltask.RecentHandler?.DbId,
                    assignedGroup = impltask.AssignedGroup,
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationTaskState, Variables)).UpdatedIdLong;
                if (udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    if (triggerActions)
                    {
                        await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task<bool> FindRuleUid(int? deviceId, string? ruleUid)
        {
            bool ruleFound = false;
            try
            {
                var Variables = new { deviceId, ruleUid };
                ruleFound = (await ApiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRuleByUid, Variables)).Count > 0;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_data"), "", true);
            }
            return ruleFound;
        }
    }
}
