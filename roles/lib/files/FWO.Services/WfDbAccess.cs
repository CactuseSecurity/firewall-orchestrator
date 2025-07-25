﻿using FWO.Data;
using FWO.Data.Workflow;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Services
{
    public class WfDbAccess(Action<Exception?, string, string, bool> DisplayMessageInUi, UserConfig UserConfig, ApiConnection ApiConnection, ActionHandler ActionHandler, bool AsAdmin)
    {
        public async Task<List<WfTicket>> FetchTickets(StateMatrix stateMatrix, List<int>? ownerIds = null, bool allStates = false, bool fullTickets = false)
        {
            List<WfTicket> tickets = [];
            try
            {
                // todo: filter own approvals, plannings...
                int fromState = allStates ? 0 : stateMatrix.LowestInputState;
                int toState = allStates ? 999 : stateMatrix.LowestEndState;

                var Variables = new { fromState, toState };
                tickets = await ApiConnection.SendQueryAsync<List<WfTicket>>(fullTickets ? RequestQueries.getFullTickets : RequestQueries.getTickets, Variables);
                if(UserConfig.ReqOwnerBased && !AsAdmin)
                {
                    tickets = await FilterWrongOwnersOut(tickets, ownerIds);
                }
                if(fullTickets)
                {
                    foreach (var ticket in tickets)
                    {
                        ticket.UpdateCidrsInTaskElements();
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return tickets;
        }

        public async Task<WfTicket?> FetchTicket(long ticketId, List<int>? ownerIds = null)
        {
            WfTicket? ticket = null;
            try
            {
                ticket = await GetTicket(ticketId);
                if(UserConfig.ReqOwnerBased && !AsAdmin)
                {
                    ticket = (await FilterWrongOwnersOut([ticket], ownerIds)).FirstOrDefault();
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
            WfTicket ticket = new ();
            try
            {
                var Variables = new { id };
                ticket = await ApiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, Variables);
                ticket.UpdateCidrsInTaskElements();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return ticket;
        }

        // Tickets

        public async Task<WfTicket> AddTicketToDb(WfTicket ticket)
        {
            try
            {
                ticket.UpdateIpStringsFromCidrInTaskElements();
                var Variables = new
                {
                    title = ticket.Title,
                    state = ticket.StateId,
                    reason = ticket.Reason,
                    requesterId = ticket.Requester?.DbId,
                    deadline = ticket.Deadline,
                    priority = ticket.Priority,
                    requestTasks = new WfTicketWriter(ticket)
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newTicket, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8001"), true);
                }
                else
                {
                    ticket = await GetTicket(returnIds[0].NewIdLong);
                    await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, ticket.Requester?.Dn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }

        public async Task<WfTicket> UpdateTicketInDb(WfTicket ticket)
        {
            try
            {
                var Variables = new
                {
                    id = ticket.Id,
                    title = ticket.Title,
                    state = ticket.StateId,
                    reason = ticket.Reason,
                    deadline = ticket.Deadline,
                    priority = ticket.Priority
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateTicket, Variables)).UpdatedIdLong;
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, ticket.Requester?.Dn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }

        // Request Tasks

        public async Task<long> AddReqTaskToDb(WfReqTask reqtask)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    title = reqtask.Title,
                    ticketId = reqtask.TicketId,
                    taskNumber = reqtask.TaskNumber,
                    state = reqtask.StateId,
                    taskType = reqtask.TaskType,
                    requestAction = reqtask.RequestAction,
                    ruleAction = reqtask.RuleAction,
                    tracking = reqtask.Tracking,
                    validFrom = reqtask.TargetBeginDate,
                    validTo = reqtask.TargetEndDate,
                    reason = reqtask.Reason,
                    additionalInfo = reqtask.AdditionalInfo,
                    freeText = reqtask.FreeText,
                    managementId = reqtask.ManagementId
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewIdLong;
                    reqtask.Id = returnId;
                    foreach(var element in reqtask.Elements)
                    {
                        element.TaskId = returnId;
                        element.Id = await AddReqElementToDb(element);
                    }
                    foreach(var approval in reqtask.Approvals)
                    {
                        approval.TaskId = returnId;
                        await AddApprovalToDb(approval);
                    }
                    foreach(var owner in reqtask.Owners)
                    {
                        await AssignOwnerInDb(returnId, owner.Owner.Id);
                    }
                    await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateReqTaskInDb(WfReqTask reqtask)
        {
            try
            {
                var Variables = new
                {
                    id = reqtask.Id,
                    title = reqtask.Title,
                    taskNumber = reqtask.TaskNumber,
                    state = reqtask.StateId,
                    taskType = reqtask.TaskType,
                    requestAction = reqtask.RequestAction,
                    ruleAction = reqtask.RuleAction,
                    tracking = reqtask.Tracking,
                    validFrom = reqtask.TargetBeginDate,
                    validTo = reqtask.TargetEndDate,
                    reason = reqtask.Reason,
                    additionalInfo = reqtask.AdditionalInfo,
                    freeText = reqtask.FreeText,
                    devices = reqtask.SelectedDevices,
                    managementId = reqtask.ManagementId
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTask, Variables)).UpdatedIdLong;
                if(udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateReqElementsInDb(reqtask);
                    await UpdateOwnersInDb(reqtask);
                    await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task UpdateReqTaskAdditionalInfo(WfReqTask reqtask)
        {
            try
            {
                var Variables = new
                {
                    id = reqtask.Id,
                    additionalInfo = reqtask.AdditionalInfo
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskAdditionalInfo, Variables)).UpdatedIdLong;
                if(udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task DeleteReqTaskFromDb(WfReqTask reqtask)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestTask, new { id = reqtask.Id })).DeletedIdLong;
                if(delId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }

        // Request Elements

        private async Task UpdateReqElementsInDb(WfReqTask reqtask)
        {
            try
            {
                foreach(var elem in reqtask.RemovedElements)
                {
                    await DeleteReqElementFromDb(elem.Id);
                }
                reqtask.RemovedElements = [];

                foreach(var element in reqtask.Elements)
                {
                    if(element.Id == 0)
                    {
                        element.Id = await AddReqElementToDb(element);
                    }
                    else
                    {
                        await UpdateReqElementInDb(element);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        private async Task<long> AddReqElementToDb(WfReqElement element)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    ipEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null,
                    port = element.Port,
                    portEnd = element.PortEnd,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    deviceId = element.DeviceId,
                    ruleUid = element.RuleUid,
                    groupName = element.GroupName,
                    name = element.Name
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewIdLong;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        private async Task UpdateReqElementInDb(WfReqElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    ipEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null,
                    port = element.Port,
                    portEnd = element.PortEnd,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    deviceId = element.DeviceId,
                    ruleUid = element.RuleUid,
                    groupName = element.GroupName,
                    name = element.Name
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElement, Variables)).UpdatedIdLong;
                if(udId != element.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_element"), UserConfig.GetText("E8007"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        private async Task DeleteReqElementFromDb(long elementId)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestElement, new { id = elementId })).DeletedIdLong;
                if(delId != elementId)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_element"), UserConfig.GetText("E8008"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_element"), "", true);
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
                    returnId = returnIds[0].NewIdLong;
                    approval.Id = returnId;
                    await ActionHandler.DoStateChangeActions(approval, WfObjectScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_approval"), "", true);
            }
            return returnId;
        }

        public async Task UpdateApprovalInDb(WfApproval approval)
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
                if(udId != approval.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_approval"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(approval, WfObjectScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_approval"), "", true);
            }
        }

        // implementation tasks

        public async Task<long> AddImplTaskToDb(WfImplTask impltask)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    title = impltask.Title,
                    reqTaskId = impltask.ReqTaskId,
                    implIaskNumber = impltask.TaskNumber,
                    state = impltask.StateId,
                    taskType = impltask.TaskType,
                    device = impltask.DeviceId,
                    implAction = impltask.ImplAction,
                    ruleAction = impltask.RuleAction,
                    tracking = impltask.Tracking,
                    handler = impltask.CurrentHandler?.DbId,
                    validFrom = impltask.TargetBeginDate,
                    validTo = impltask.TargetEndDate,
                    freeText = impltask.FreeText,
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newImplementationTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewIdLong;
                    impltask.Id = returnId;
                    foreach(var element in impltask.ImplElements)
                    {
                        element.ImplTaskId = returnId;
                        element.Id = await AddImplElementToDb(element);
                    }
                    foreach(var comment in impltask.Comments)
                    {
                        comment.Comment.Id = await AddCommentToDb(comment.Comment);
                        if(comment.Comment.Id != 0)
                        {
                            await AssignCommentToImplTaskInDb(returnId, comment.Comment.Id);
                        }
                    }
                    await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateImplTaskInDb(WfImplTask impltask, WfReqTask reqtask)
        {
            try
            {
                var Variables = new
                {
                    id = impltask.Id,
                    title = impltask.Title,
                    reqTaskId = impltask.ReqTaskId,
                    implIaskNumber = impltask.TaskNumber,
                    state = impltask.StateId,
                    taskType = impltask.TaskType,
                    device = impltask.DeviceId,
                    implAction = impltask.ImplAction,
                    ruleAction = impltask.RuleAction,
                    tracking = impltask.Tracking,
                    handler = impltask.CurrentHandler?.DbId,
                    validFrom = impltask.TargetBeginDate,
                    validTo = impltask.TargetEndDate,
                    freeText = impltask.FreeText,
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationTask, Variables)).UpdatedIdLong;
                if(udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateImplElementsInDb(impltask);
                    await UpdateOwnersInDb(reqtask);
                    await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task DeleteImplTaskFromDb(WfImplTask impltask)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationTask, new { id = impltask.Id })).DeletedIdLong;
                if(delId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }


        // implementation elements

        private async Task UpdateImplElementsInDb(WfImplTask impltask)
        {
            try
            {
                foreach(var elem in impltask.RemovedElements)
                {
                    await DeleteImplElementFromDb(elem.Id);
                }
                impltask.RemovedElements = [];

                foreach(var element in impltask.ImplElements)
                {
                    if(element.Id == 0)
                    {
                        element.Id = await AddImplElementToDb(element);
                    }
                    else
                    {
                        await UpdateImplElementInDb(element);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        private async Task<long> AddImplElementToDb(WfImplElement element)
        {
            long returnId = 0;
            try
            {
                var Variables = new
                {
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    ipEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null,
                    port = element.Port,
                    portEnd = element.PortEnd,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    ruleUid = element.RuleUid,
                    groupName = element.GroupName,
                    name = element.Name
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newImplementationElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewIdLong;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        private async Task UpdateImplElementInDb(WfImplElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    ipEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null,
                    port = element.Port,
                    portEnd = element.PortEnd,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    ruleUid = element.RuleUid,
                    groupName = element.GroupName,
                    name = element.Name
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationElement, Variables)).UpdatedIdLong;
                if(udId != element.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_element"), UserConfig.GetText("E8007"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        private async Task DeleteImplElementFromDb(long elementId)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationElement, new { id = elementId })).DeletedIdLong;
                if(delId != elementId)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_element"), UserConfig.GetText("E8008"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_element"), "", true);
            }
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
            try
            {
                var Variables = new { ticketId, commentId };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.addCommentToTicket, Variables)).ReturnIds;
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

        public async Task AssignCommentToReqTaskInDb(long taskId, long commentId)
        {
            try
            {
                var Variables = new { taskId, commentId };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.addCommentToReqTask, Variables)).ReturnIds;
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

        public async Task AssignCommentToImplTaskInDb(long taskId, long commentId)
        {
            try
            {
                var Variables = new { taskId, commentId };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.addCommentToImplTask, Variables)).ReturnIds;
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

        public async Task AssignCommentToApprovalInDb(long approvalId, long commentId)
        {
            try
            {
                var Variables = new { approvalId, commentId };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.addCommentToApproval, Variables)).ReturnIds;
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
                foreach(var owner in reqtask.RemovedOwners)
                {
                    await RemoveOwnerInDb(reqtask.Id, owner.Id);
                    FwoOwnerDataHelper? oldOwner = reqtask.Owners.FirstOrDefault(x => x.Owner.Id == owner.Id);
                    if(oldOwner != null)
                    {
                        reqtask.Owners.Remove(oldOwner);
                    }
                }
                reqtask.RemovedOwners = [];

                foreach(var owner in reqtask.NewOwners)
                {
                    await AssignOwnerInDb(reqtask.Id, owner.Id);
                    reqtask.Owners.Add(new(){ Owner = owner });
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

        public async Task UpdateTicketStateInDb(WfTicket ticket)
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
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, WfObjectScopes.Ticket, null, ticket.Id, ticket.Requester?.Dn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
        }

        public async Task UpdateReqTaskStateInDb(WfReqTask reqtask)
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
                if(udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task UpdateImplTaskStateInDb(WfImplTask impltask)
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
                if(udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
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
