using System.Collections.Generic;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        /// <summary>
        /// Creates an implementation task and persists its nested implementation elements.
        /// </summary>
        public async Task<long> AddImplTaskToDb(WfImplTask impltask)
        {
            long returnId = 0;
            try
            {
                var variables = BuildImplTaskInsertVariables(impltask);
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newImplementationTask, variables)).ReturnIds;
                if (returnIds == null)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("add_task"), UserConfig.GetText("E8003"), true);
                }
                else
                {
                    int newStateId = impltask.StateId;
                    returnId = returnIds[0].NewIdLong;
                    impltask.Id = returnId;
                    foreach (var element in impltask.ImplElements)
                    {
                        element.ImplTaskId = returnId;
                        element.Id = await AddImplElementToDb(element);
                    }
                    foreach (var comment in impltask.Comments)
                    {
                        comment.Comment.Id = await AddCommentToDb(comment.Comment);
                        if (comment.Comment.Id != 0)
                        {
                            await AssignCommentToImplTaskInDb(returnId, comment.Comment.Id);
                        }
                    }
                    impltask.MarkCreatedStateChanged(newStateId);
                    await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("add_task"), "", true);
            }
            return returnId;
        }

        /// <summary>
        /// Updates an implementation task and reconciles its nested implementation elements.
        /// </summary>
        public async Task UpdateImplTaskInDb(WfImplTask impltask, WfReqTask reqtask)
        {
            WfTicket previousTicket = await GetTicket(reqtask.TicketId);
            WfImplTask? previousTask = previousTicket.Tasks
                .SelectMany(task => task.ImplementationTasks)
                .FirstOrDefault(task => task.Id == impltask.Id);
            try
            {
                var variables = BuildImplTaskUpdateVariables(impltask);
                variables["id"] = impltask.Id;
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationTask, variables)).UpdatedIdLong;
                if (udId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText("E8004"), true);
                }
                else
                {
                    await UpdateImplElementsInDb(impltask);
                    await UpdateOwnersInDb(reqtask);
                    if (previousTask != null)
                    {
                        await LogWorkflowChange(reqtask.TicketId, ChangeHistoryObjectType.ImplementationTask, impltask.Id,
                            "Updated workflow implementation task", ImplementationTaskHistorySnapshot(previousTask), ImplementationTaskHistorySnapshot(impltask), previousTicket.Requester);
                    }
                    await ActionHandler.DoStateChangeActions(impltask, WfObjectScopes.ImplementationTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("save_task"), "", true);
            }
        }

        /// <summary>
        /// Deletes an implementation task from the database.
        /// </summary>
        public async Task DeleteImplTaskFromDb(WfImplTask impltask)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationTask, new { id = impltask.Id })).DeletedIdLong;
                if (delId != impltask.Id)
                {
                    DisplayMessageInUi(null, UserConfig.GetText("delete_task"), UserConfig.GetText("E8005"), true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, UserConfig.GetText("delete_task"), "", true);
            }
        }

        private static Dictionary<string, object?> BuildImplTaskInsertVariables(WfImplTask impltask)
        {
            Dictionary<string, object?> variables = BuildImplTaskUpdateVariables(impltask);
            variables["taskType"] = impltask.TaskType;
            return variables;
        }

        private static Dictionary<string, object?> BuildImplTaskUpdateVariables(WfImplTask impltask)
        {
            return new Dictionary<string, object?>
            {
                ["title"] = impltask.Title,
                ["reqTaskId"] = impltask.ReqTaskId,
                ["implIaskNumber"] = impltask.TaskNumber,
                ["state"] = impltask.StateId,
                ["device"] = impltask.DeviceId,
                ["implAction"] = impltask.ImplAction,
                ["ruleAction"] = impltask.RuleAction,
                ["tracking"] = impltask.Tracking,
                ["handler"] = impltask.CurrentHandler?.DbId,
                ["validFrom"] = impltask.TargetBeginDate,
                ["validTo"] = impltask.TargetEndDate,
                ["freeText"] = impltask.FreeText
            };
        }

        private async Task UpdateImplElementsInDb(WfImplTask impltask)
        {
            try
            {
                foreach (var elem in impltask.RemovedElements)
                {
                    await DeleteImplElementFromDb(elem.Id);
                }
                impltask.RemovedElements = [];

                foreach (var element in impltask.ImplElements)
                {
                    if (element.Id == 0)
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
                var variables = BuildImplElementVariables(element);
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<ReturnIdWrapper>(RequestQueries.newImplementationElement, variables)).ReturnIds;
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
                var variables = BuildImplElementVariables(element);
                variables["id"] = element.Id;
                long udId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateImplementationElement, variables)).UpdatedIdLong;
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

        private async Task DeleteImplElementFromDb(long elementId)
        {
            try
            {
                long delId = (await ApiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteImplementationElement, new { id = elementId })).DeletedIdLong;
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

        private static Dictionary<string, object?> BuildImplElementVariables(WfImplElement element)
        {
            var variables = BuildElementBaseVariables(element, element.Cidr, element.CidrEnd);
            variables["implementationAction"] = element.ImplAction;
            variables["implTaskId"] = element.ImplTaskId;
            return variables;
        }
    }
}
