using System.Collections.Generic;
using System.Linq;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        /// <summary>
        /// Creates a request task and persists its nested request elements.
        /// </summary>
        public async Task<long> AddReqTaskToDb(WfReqTask reqtask)
        {
            long returnId = 0;
            try
            {
                var variables = BuildReqTaskInsertVariables(reqtask);
                variables["ticketId"] = reqtask.TicketId;
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestTask, variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    int newStateId = reqtask.StateId;
                    returnId = returnIds[0].NewIdLong;
                    reqtask.Id = returnId;
                    foreach (var element in reqtask.Elements)
                    {
                        element.TaskId = returnId;
                        element.Id = await AddReqElementToDb(element);
                    }
                    foreach (var approval in reqtask.Approvals)
                    {
                        approval.TaskId = returnId;
                        await AddApprovalToDb(approval);
                    }
                    foreach (var owner in reqtask.Owners)
                    {
                        await AssignOwnerInDb(returnId, owner.Owner.Id);
                    }
                    reqtask.MarkCreatedStateChanged(newStateId);
                    await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        /// <summary>
        /// Updates a request task and synchronizes its request elements and owners.
        /// </summary>
        public async Task UpdateReqTaskInDb(WfReqTask reqtask)
        {
            if (reqtask.Locked)
            {
                return;
            }

            WfTicket previousTicket = await GetTicket(reqtask.TicketId);
            WfReqTask? previousTask = previousTicket.Tasks.FirstOrDefault(task => task.Id == reqtask.Id);
            try
            {
                var variables = BuildReqTaskUpdateVariables(reqtask);
                variables["id"] = reqtask.Id;
                variables["devices"] = reqtask.SelectedDevices;
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTask, variables)).UpdatedIdLong;
                if (udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateReqElementsInDb(reqtask);
                    await UpdateOwnersInDb(reqtask);
                    if (previousTask != null)
                    {
                        await LogWorkflowChange(reqtask.TicketId, ChangeHistoryObjectType.RequestTask, reqtask.Id,
                            "Updated workflow request task", RequestTaskHistorySnapshot(previousTask), RequestTaskHistorySnapshot(reqtask), previousTicket.Requester);
                    }
                    await ActionHandler.DoStateChangeActions(reqtask, WfObjectScopes.RequestTask, reqtask.Owners.Count > 0 ? reqtask.Owners.First().Owner : null, reqtask.TicketId);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        /// <summary>
        /// Updates the additional info field of a request task.
        /// </summary>
        public async Task UpdateReqTaskAdditionalInfo(WfReqTask reqtask)
        {
            try
            {
                var variables = new
                {
                    id = reqtask.Id,
                    additionalInfo = reqtask.AdditionalInfo
                };
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskAdditionalInfo, variables)).UpdatedIdLong;
                if (udId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        /// <summary>
        /// Deletes a request task.
        /// </summary>
        public async Task DeleteReqTaskFromDb(WfReqTask reqtask)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestTask, new { id = reqtask.Id })).DeletedIdLong;
                if (delId != reqtask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }

        /// <summary>
        /// Builds the insert payload for request tasks.
        /// </summary>
        private static Dictionary<string, object?> BuildReqTaskInsertVariables(WfReqTask reqtask)
        {
            Dictionary<string, object?> variables = BuildReqTaskUpdateVariables(reqtask);
            variables["taskNumber"] = reqtask.TaskNumber;
            variables["taskType"] = reqtask.TaskType;
            variables["locked"] = reqtask.Locked;
            return variables;
        }

        /// <summary>
        /// Builds the update payload for request tasks.
        /// </summary>
        private static Dictionary<string, object?> BuildReqTaskUpdateVariables(WfReqTask reqtask)
        {
            return new Dictionary<string, object?>
            {
                ["title"] = reqtask.Title,
                ["state"] = reqtask.StateId,
                ["requestAction"] = reqtask.RequestAction,
                ["ruleAction"] = reqtask.RuleAction,
                ["tracking"] = reqtask.Tracking,
                ["validFrom"] = reqtask.TargetBeginDate,
                ["validTo"] = reqtask.TargetEndDate,
                ["reason"] = reqtask.Reason,
                ["additionalInfo"] = reqtask.AdditionalInfo,
                ["freeText"] = reqtask.FreeText,
                ["managementId"] = reqtask.ManagementId
            };
        }

        /// <summary>
        /// Synchronizes request task elements with the database.
        /// </summary>
        private async Task UpdateReqElementsInDb(WfReqTask reqtask)
        {
            try
            {
                foreach (var elem in reqtask.RemovedElements)
                {
                    await DeleteReqElementFromDb(elem.Id);
                }
                reqtask.RemovedElements = [];

                foreach (var element in reqtask.Elements)
                {
                    if (element.Id == 0)
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

        /// <summary>
        /// Adds a request element.
        /// </summary>
        private async Task<long> AddReqElementToDb(WfReqElement element)
        {
            long returnId = 0;
            try
            {
                var variables = BuildReqElementVariables(element);
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newRequestElement, variables)).ReturnIds;
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

        /// <summary>
        /// Updates a request element.
        /// </summary>
        private async Task UpdateReqElementInDb(WfReqElement element)
        {
            try
            {
                var variables = BuildReqElementVariables(element);
                variables["id"] = element.Id;
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElement, variables)).UpdatedIdLong;
                if (udId != element.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_element"), UserConfig.GetText("E8007"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_element"), "", true);
            }
        }

        /// <summary>
        /// Deletes a request element.
        /// </summary>
        private async Task DeleteReqElementFromDb(long elementId)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteRequestElement, new { id = elementId })).DeletedIdLong;
                if (delId != elementId)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_element"), UserConfig.GetText("E8008"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_element"), "", true);
            }
        }

        /// <summary>
        /// Builds the insert/update payload for request elements.
        /// </summary>
        private static Dictionary<string, object?> BuildReqElementVariables(WfReqElement element)
        {
            var variables = BuildElementBaseVariables(element, element.Cidr, element.CidrEnd);
            variables["requestAction"] = element.RequestAction;
            variables["taskId"] = element.TaskId;
            variables["deviceId"] = element.DeviceId;
            variables["flowNwObjId"] = element.FlowNetworkObjectId;
            variables["flowNwGrpId"] = element.FlowNetworkGroupId;
            variables["flowSvcObjId"] = element.FlowServiceObjectId;
            variables["flowSvcGrpId"] = element.FlowServiceGroupId;
            return variables;
        }
    }
}
