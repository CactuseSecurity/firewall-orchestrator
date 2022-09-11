using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;

namespace FWO.Ui.Services
{
    public class RequestDbAccess
    {
        private Action<Exception?, string, string, bool>? DisplayMessageInUi;
        private UserConfig UserConfig;
        private ApiConnection ApiConnection;
        private ActionHandler ActionHandler;


        public RequestDbAccess(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig, ApiConnection apiConnection, ActionHandler actionHandler)
        {
            DisplayMessageInUi = displayMessageInUi;
            UserConfig = userConfig;
            ApiConnection = apiConnection;
            ActionHandler = actionHandler;
        }

        public async Task<List<RequestTicket>> FetchTickets(StateMatrix stateMatrix, int viewOpt = 0)
        {
            List<RequestTicket> requests = new List<RequestTicket>();
            try
            {
                // todo: filter own approvals, plannings...
                var Variables = new
                {
                    from_state = stateMatrix.LowestInputState,
                    to_state = (viewOpt == 0 ? stateMatrix.LowestEndState : 999)
                };
                requests = await ApiConnection.SendQueryAsync<List<RequestTicket>>(FWO.Api.Client.Queries.RequestQueries.getTickets, Variables);
                foreach (var ticket in requests)
                {
                    ticket.UpdateCidrsInTaskElements();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return requests;
        }

        public async Task<RequestTicket> GetTicket(int id)
        {
            RequestTicket ticket = new RequestTicket();
            try
            {
                var Variables = new
                {
                    id = id,
                };
                ticket = await ApiConnection.SendQueryAsync<RequestTicket>(FWO.Api.Client.Queries.RequestQueries.getTicketById, Variables);
                ticket.UpdateCidrsInTaskElements();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("fetch_requests"), "", true);
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
                    requestTasks = new RequestTicketWriter(ticket)
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newTicket, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8001"), true);
                }
                else
                {
                    ticket = await GetTicket(returnIds[0].NewId);
                    await ActionHandler.DoStateChangeActions(ticket, ActionScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
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
                    reason = ticket.Reason
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateTicket, Variables)).UpdatedId;
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, ActionScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
            }
            return ticket;
        }

        // Request Tasks

        public async Task<int> AddReqTaskToDb(RequestTask task)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    title = task.Title,
                    ticketId = task.TicketId,
                    taskNumber = task.TaskNumber,
                    state = task.StateId,
                    taskType = task.TaskType,
                    requestAction = task.RequestAction,
                    ruleAction = task.RuleAction,
                    tracking = task.Tracking,
                    validFrom = task.TargetBeginDate,
                    validTo = task.TargetEndDate,
                    reason = task.Reason
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newRequestTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                    foreach(var element in task.Elements)
                    {
                        element.TaskId = returnId;
                        element.Id = await AddReqElementToDb(element);
                    }
                    foreach(var approval in task.Approvals)
                    {
                        approval.TaskId = returnId;
                        await AddApprovalToDb(approval);
                    }
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.RequestTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateReqTaskInDb(RequestTask task)
        {
            try
            {
                var Variables = new
                {
                    id = task.Id,
                    title = task.Title,
                    taskNumber = task.TaskNumber,
                    state = task.StateId,
                    taskType = task.TaskType,
                    requestAction = task.RequestAction,
                    ruleAction = task.RuleAction,
                    tracking = task.Tracking,
                    validFrom = task.TargetBeginDate,
                    validTo = task.TargetEndDate,
                    reason = task.Reason,
                    deviceId = task.DeviceId
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateRequestTask, Variables)).UpdatedId;
                if(udId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    foreach(var elem in task.RemovedElements)
                    {
                        await DeleteReqElementFromDb(elem.Id);
                    }
                    task.RemovedElements = new List<RequestElement>();

                    foreach(var element in task.Elements)
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
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.RequestTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task DeleteReqTaskFromDb(RequestTask task)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.deleteRequestTask, new { id = task.Id })).DeletedId;
                if(delId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }

        // Request Elements

        public async Task<int> AddReqElementToDb(RequestElement element)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = (element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null),
                    port = element.Port,
                    proto = element.ProtoId,
                    network_obj_id = element.NetworkId,
                    service_id = element.ServiceId,
                    field = element.Field,
                    user_id = element.UserId,
                    original_nat_id = element.OriginalNatId
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newRequestElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        public async Task UpdateReqElementInDb(RequestElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    requestAction = element.RequestAction,
                    taskId = element.TaskId,
                    ip = (element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null),
                    port = element.Port,
                    proto = element.ProtoId,
                    network_obj_id = element.NetworkId,
                    service_id = element.ServiceId,
                    field = element.Field,
                    user_id = element.UserId,
                    original_nat_id = element.OriginalNatId
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateRequestElement, Variables)).UpdatedId;
                if(udId != element.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_element"), UserConfig.GetText("E8007"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        public async Task DeleteReqElementFromDb(int elementId)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.deleteRequestElement, new { id = elementId })).DeletedId;
                if(delId != elementId)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("delete_element"), UserConfig.GetText("E8008"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("delete_element"), "", true);
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newApproval, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("add_approval"), UserConfig.GetText("E8009"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                    await ActionHandler.DoStateChangeActions(approval, ActionScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("add_approval"), "", true);
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
                    comment = approval.Comment
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateApproval, Variables)).UpdatedId;
                if(udId != approval.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_approval"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(approval, ActionScopes.Approval);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_approval"), "", true);
            }
        }

        // implementation tasks

        public async Task<int> AddImplTaskToDb(ImplementationTask task)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    reqTaskId = task.ReqTaskId,
                    implIaskNumber = task.ImplTaskNumber,
                    state = task.StateId,
                    taskType = task.TaskType,
                    device = task.DeviceId,
                    implAction = task.ImplAction,
                    ruleAction = task.RuleAction,
                    tracking = task.Tracking,
                    handler = task.CurrentHandler?.DbId,
                    validFrom = task.TargetBeginDate,
                    validTo = task.TargetEndDate,
                    comment = task.FwAdminComments
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newImplementationTask, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                    foreach(var element in task.ImplElements)
                    {
                        element.ImplTaskId = returnId;
                        element.Id = await AddImplElementToDb(element);
                    }
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        public async Task UpdateImplTaskInDb(ImplementationTask task)
        {
            try
            {
                var Variables = new
                {
                    id = task.Id,
                    reqTaskId = task.ReqTaskId,
                    implIaskNumber = task.ImplTaskNumber,
                    state = task.StateId,
                    taskType = task.TaskType,
                    device = task.DeviceId,
                    implAction = task.ImplAction,
                    ruleAction = task.RuleAction,
                    tracking = task.Tracking,
                    handler = task.CurrentHandler?.DbId,
                    validFrom = task.TargetBeginDate,
                    validTo = task.TargetEndDate,
                    comment = task.FwAdminComments
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateImplementationTask, Variables)).UpdatedId;
                if(udId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    foreach(var elem in task.RemovedElements)
                    {
                        await DeleteImplElementFromDb(elem.Id);
                    }
                    task.RemovedElements = new List<ImplementationElement>();

                    foreach(var element in task.ImplElements)
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
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task DeleteImplTaskFromDb(ImplementationTask task)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.deleteImplementationTask, new { id = task.Id })).DeletedId;
                if(delId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }


        // implementation elements

        public async Task<int> AddImplElementToDb(ImplementationElement element)
        {
            int returnId = 0;
            try
            {
                var Variables = new
                {
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = (element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null),
                    port = element.Port,
                    proto = element.ProtoId,
                    network_obj_id = element.NetworkId,
                    service_id = element.ServiceId,
                    field = element.Field,
                    user_id = element.UserId,
                    original_nat_id = element.OriginalNatId
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RequestQueries.newImplementationElement, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("add_element"), UserConfig.GetText("E8006"), true);
                }
                else
                {
                    returnId = returnIds[0].NewId;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("add_element"), "", true);
            }
            return returnId;
        }

        public async Task UpdateImplElementInDb(ImplementationElement element)
        {
            try
            {
                var Variables = new
                {
                    id = element.Id,                
                    implementationAction = element.ImplAction,
                    implTaskId = element.ImplTaskId,
                    ip = (element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null),
                    port = element.Port,
                    proto = element.ProtoId,
                    network_obj_id = element.NetworkId,
                    service_id = element.ServiceId,
                    field = element.Field,
                    user_id = element.UserId,
                    original_nat_id = element.OriginalNatId
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateImplementationElement, Variables)).UpdatedId;
                if(udId != element.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_element"), UserConfig.GetText("E8007"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        public async Task DeleteImplElementFromDb(int elementId)
        {
            try
            {
                int delId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.deleteImplementationElement, new { id = elementId })).DeletedId;
                if(delId != elementId)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("delete_element"), UserConfig.GetText("E8008"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("delete_element"), "", true);
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
                    closed = ticket.CompletionDate
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateTicketState, Variables)).UpdatedId;
                if(udId != ticket.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_request"), UserConfig.GetText("E8002"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(ticket, ActionScopes.Ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
            }
        }

        public async Task UpdateReqTaskStateInDb(RequestTask task)
        {
            try
            {
                var Variables = new
                {
                    id = task.Id,
                    state = task.StateId,
                    start = task.Start,
                    stop = task.Stop,
                    handler = task.CurrentHandler?.DbId,
                    recentHandler = task.RecentHandler?.DbId,
                    assignedGroup = task.AssignedGroup,
                    comment = task.FwAdminComments
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateRequestTaskState, Variables)).UpdatedId;
                if(udId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.RequestTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        public async Task UpdateImplTaskStateInDb(ImplementationTask task)
        {
            try
            {
                var Variables = new
                {
                    id = task.Id,
                    state = task.StateId,
                    start = task.Start,
                    stop = task.Stop,
                    handler = task.CurrentHandler?.DbId
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateImplementationTaskState, Variables)).UpdatedId;
                if(udId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await ActionHandler.DoStateChangeActions(task, ActionScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_task"), "", true);
            }
        }
    }
}
