using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Ui.Services
{
    public class RequestDbAccess
    {
        private Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        private readonly UserConfig UserConfig;
        private readonly ApiConnection ApiConnection;
        private readonly ActionHandler ActionHandler;


        public RequestDbAccess(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig, ApiConnection apiConnection, ActionHandler actionHandler)
        {
            DisplayMessageInUi = displayMessageInUi;
            UserConfig = userConfig;
            ApiConnection = apiConnection;
            ActionHandler = actionHandler;
        }

        public async Task<List<RequestTicket>> FetchTickets(StateMatrix stateMatrix, List<int> ownerIds, bool allStates = false, bool ignoreOwners = false)
        {
            List<RequestTicket> tickets = [];
            try
            {
                // todo: filter own approvals, plannings...
                int fromState = allStates ? 0 : stateMatrix.LowestInputState;
                int toState = allStates ? 999 : stateMatrix.LowestEndState;

                var Variables = new { fromState, toState };
                tickets = await ApiConnection.SendQueryAsync<List<RequestTicket>>(RequestQueries.getTickets, Variables);
                if(UserConfig.ReqOwnerBased && ! ignoreOwners)
                {
                    tickets = FilterWrongOwnersOut(tickets, ownerIds);
                }
                foreach (var ticket in tickets)
                {
                    ticket.UpdateCidrsInTaskElements();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return tickets;
        }

        private static List<RequestTicket> FilterWrongOwnersOut(List<RequestTicket> ticketsIn, List<int> ownerIds)
        {
            List<RequestTicket> ticketsOut = [];
            foreach(var ticket in ticketsIn)
            {
                if(ticket.Tasks.FirstOrDefault(ta => ta.TaskType != TaskType.new_interface.ToString()) != null
                    || ticket.Tasks.FirstOrDefault(ta => ta.Owners.Count == 0) != null)
                {
                    ticketsOut.Add(ticket);
                }
                else
                {
                    foreach(var task in ticket.Tasks)
                    {
                        if(task.Owners.FirstOrDefault(x => ownerIds.Contains(x.Owner.Id)) != null)
                        {
                            ticketsOut.Add(ticket);
                            break;
                        }
                    }
                }
            }
            return ticketsOut;
        }

        public async Task<RequestTicket> GetTicket(int id)
        {
            RequestTicket ticket = new ();
            try
            {
                var Variables = new { id };
                ticket = await ApiConnection.SendQueryAsync<RequestTicket>(RequestQueries.getTicketById, Variables);
                ticket.UpdateCidrsInTaskElements();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return ticket;
        }

        // Tickets

        public async Task<RequestTicket> AddTicketToDb(RequestTicket ticket)
        {
            try
            {
                ticket.UpdateCidrStringsInTaskElements();
                var Variables = new
                {
                    title = ticket.Title,
                    state = ticket.StateId,
                    reason = ticket.Reason,
                    requesterId = ticket.Requester?.DbId,
                    deadline = ticket.Deadline,
                    priority = ticket.Priority,
                    requestTasks = new RequestTicketWriter(ticket)
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newTicket, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8001"), true);
                }
                else
                {
                    ticket = await GetTicket(returnIds[0].NewId);
                    await ActionHandler.DoStateChangeActions(ticket, RequestObjectScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }

        public async Task<RequestTicket> UpdateTicketInDb(RequestTicket ticket)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateTicket, Variables)).UpdatedId;
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, RequestObjectScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }

        // Request Tasks

        public async Task<int> AddReqTaskToDb(RequestReqTask reqtask)
        {
            int returnId = 0;
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
                    freeText = reqtask.FreeText
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newRequestTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
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
                    await ActionHandler.DoStateChangeActions(reqtask, RequestObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateReqTaskInDb(RequestReqTask reqtask)
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
                    devices = reqtask.SelectedDevices
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTask, Variables)).UpdatedId;
                if(udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateReqElementsInDb(reqtask);
                    await UpdateOwnersInDb(reqtask);
                    await ActionHandler.DoStateChangeActions(reqtask, RequestObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task UpdateReqTaskAdditionalInfo(RequestReqTask reqtask)
        {
            try
            {
                var Variables = new
                {
                    id = reqtask.Id,
                    additionalInfo = reqtask.AdditionalInfo
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskAdditionalInfo, Variables)).UpdatedId;
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

        public async Task DeleteReqTaskFromDb(RequestReqTask reqtask)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestTask, new { id = reqtask.Id })).DeletedId;
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

        private async Task UpdateReqElementsInDb(RequestReqTask reqtask)
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

        private async Task<int> AddReqElementToDb(RequestReqElement element)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    port = element.Port,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    deviceId = element.DeviceId,
                    ruleUid = element.RuleUid
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newRequestElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        private async Task UpdateReqElementInDb(RequestReqElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    port = element.Port,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    deviceId = element.DeviceId,
                    ruleUid = element.RuleUid
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElement, Variables)).UpdatedId;
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
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestElement, new { id = elementId })).DeletedId;
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

        public async Task<int> AddApprovalToDb(RequestApproval approval)
        {
            int returnId = 0;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newApproval, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_approval"), UserConfig.GetText("E8009"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                    approval.Id = returnId;
                    await ActionHandler.DoStateChangeActions(approval, RequestObjectScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_approval"), "", true);
            }
            return returnId;
        }

        public async Task UpdateApprovalInDb(RequestApproval approval)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateApproval, Variables)).UpdatedId;
                if(udId != approval.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_approval"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(approval, RequestObjectScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_approval"), "", true);
            }
        }

        // implementation tasks

        public async Task<int> AddImplTaskToDb(RequestImplTask impltask)
        {
            int returnId = 0;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newImplementationTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
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
                    await ActionHandler.DoStateChangeActions(impltask, RequestObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateImplTaskInDb(RequestImplTask impltask, RequestReqTask reqtask)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationTask, Variables)).UpdatedId;
                if(udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateImplElementsInDb(impltask);
                    await UpdateOwnersInDb(reqtask);
                    await ActionHandler.DoStateChangeActions(impltask, RequestObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task DeleteImplTaskFromDb(RequestImplTask impltask)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationTask, new { id = impltask.Id })).DeletedId;
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

        private async Task UpdateImplElementsInDb(RequestImplTask impltask)
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

        private async Task<int> AddImplElementToDb(RequestImplElement element)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    port = element.Port,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    ruleUid = element.RuleUid
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newImplementationElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        private async Task UpdateImplElementInDb(RequestImplElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
                    port = element.Port,
                    proto = element.ProtoId,
                    networkObjId = element.NetworkId,
                    serviceId = element.ServiceId,
                    field = element.Field,
                    userId = element.UserId,
                    originalNatId = element.OriginalNatId,
                    ruleUid = element.RuleUid
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationElement, Variables)).UpdatedId;
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
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationElement, new { id = elementId })).DeletedId;
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

        public async Task<int> AddCommentToDb(RequestComment comment)
        {
            int returnId = 0;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.newComment, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_comment"), UserConfig.GetText("E8012"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.addCommentToTicket, Variables)).ReturnIds;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.addCommentToReqTask, Variables)).ReturnIds;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.addCommentToImplTask, Variables)).ReturnIds;
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.addCommentToApproval, Variables)).ReturnIds;
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

        public async Task UpdateOwnersInDb(RequestReqTask reqtask)
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
                reqtask.RemovedOwners = new ();

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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(RequestQueries.addOwnerToReqTask, Variables)).ReturnIds;
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

        public async Task UpdateTicketStateInDb(RequestTicket ticket)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateTicketState, Variables)).UpdatedId;
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, RequestObjectScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_request"), "", true);
            }
        }

        public async Task UpdateReqTaskStateInDb(RequestReqTask reqtask)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskState, Variables)).UpdatedId;
                if(udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(reqtask, RequestObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task UpdateImplTaskStateInDb(RequestImplTask impltask)
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
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationTaskState, Variables)).UpdatedId;
                if(udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(impltask, RequestObjectScopes.ImplementationTask);
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
