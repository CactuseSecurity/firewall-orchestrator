using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;

namespace FWO.Ui.Services
{
    public class RequestDbAccess
    {
        Action<Exception?, string, string, bool>? DisplayMessageInUi;
        UserConfig UserConfig;
        ApiConnection ApiConnection;


        public RequestDbAccess(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig, ApiConnection apiConnection)
        {
            DisplayMessageInUi = displayMessageInUi;
            UserConfig = userConfig;
            ApiConnection = apiConnection;
        }

        public async Task<List<RequestTicket>> FetchTickets(StateMatrix stateMatrix)
        {
            List<RequestTicket> requests = new List<RequestTicket>();
            try
            {
                // todo: filter own approvals, plannings...
                var Variables = new
                {
                    from_state = stateMatrix.LowestInputState,
                    to_state = stateMatrix.LowestEndState,
                };
                requests = await ApiConnection.SendQueryAsync<List<RequestTicket>>(FWO.Api.Client.Queries.RequestQueries.getTickets, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("fetch_requests"), "", true);
            }
            return requests;
        }

        // Tickets

        public async Task<List<RequestTicket>> AddTicketToDb(RequestTicket ticket, List<RequestTicket> requests)
        {
            try
            {
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
                    ticket.Id = returnIds[0].NewId;
                    requests.Add(ticket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
            }
            return requests;
        }

        public async Task<List<RequestTicket>> UpdateTicketInDb(RequestTicket ticket, List<RequestTicket> requests)
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
                    foreach(RequestTask task in ticket.Tasks)
                    {
                        task.StateId = ticket.StateId;
                        await UpdateReqTaskStateInDb(task);
                    }
                    requests[requests.FindIndex(x => x.Id == ticket.Id)] = ticket;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
            }
            return requests;
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
                    reason = task.Reason
                };
                int udId = (await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.RequestQueries.updateRequestTask, Variables)).UpdatedId;
                if(udId != task.Id)
                {
                    DisplayMessageInUi!(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    foreach(var element in task.Elements)
                    {
                        await UpdateReqElementInDb(element);
                    }
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
                    ip = element.Ip.ToCidrString(),
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
                    ip = element.Ip.ToCidrString(),
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
                    foreach(var element in task.ImplElements)
                    {
                        await UpdateImplElementInDb(element);
                    }
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
                    ip = element.Ip.ToCidrString(),
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
                    ip = element.Ip.ToCidrString(),
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

        // State changes

        public async Task UpdateTaskStateFromApprovals(RequestTask reqTask, StateMatrix stateMatrix)
        {
            List<int> approvalStates = new List<int>();
            foreach (var approval in reqTask.Approvals)
            {
                approvalStates.Add(approval.StateId);
            }
            if (approvalStates.Count > 0)
            {
                reqTask.StateId = stateMatrix.getDerivedStateFromSubStates(approvalStates);
            }
            await UpdateReqTaskStateInDb(reqTask);
        }

        public async Task UpdateTicketStateFromImplTasks(RequestTicket ticket, List<RequestTicket> requests, StateMatrix stateMatrix)
        {
            List<int> taskStates = new List<int>();
            foreach (RequestTask reqTask in ticket.Tasks)
            {
                await UpdateReqTaskStateFromImplTasks(reqTask, stateMatrix);
            }
            await UpdateTicketStateFromTasks(ticket, requests, stateMatrix);
        }

        public async Task UpdateReqTaskStateFromImplTasks(RequestTask reqTask, StateMatrix stateMatrix)
        {
            List<int> implTaskStates = new List<int>();
            foreach (var implTask in reqTask.ImplementationTasks)
            {
                implTaskStates.Add(implTask.StateId);
            }
            if (implTaskStates.Count > 0)
            {
                reqTask.StateId = stateMatrix.getDerivedStateFromSubStates(implTaskStates);
            }
            await UpdateReqTaskStateInDb(reqTask);
        }

        public async Task UpdateTicketStateFromTasks(RequestTicket ticket, List<RequestTicket> requests, StateMatrix stateMatrix)
        {
            List<int> taskStates = new List<int>();
            foreach (RequestTask tsk in ticket.Tasks)
            {
                taskStates.Add(tsk.StateId);
            }
            ticket.StateId = stateMatrix.getDerivedStateFromSubStates(taskStates);
            if (stateMatrix.IsLastActivePhase && ticket.StateId >= stateMatrix.LowestEndState)
            {
                ticket.CompletionDate = DateTime.Now;
            }

            await UpdateTicketStateInDb(ticket, requests);
        }

        public async Task<List<RequestTicket>> UpdateTicketStateInDb(RequestTicket ticket, List<RequestTicket> requests)
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
                    requests[requests.FindIndex(x => x.Id == ticket.Id)] = ticket;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_request"), "", true);
            }
            return requests;
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
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, UserConfig.GetText("save_task"), "", true);
            }
        }
    }
}
