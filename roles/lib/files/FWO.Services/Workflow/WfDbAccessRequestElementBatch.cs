using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

public partial class WfDbAccess
{
    /// <summary>
    /// Persists the planned effective snapshots in one narrow batch and applies the submitted values in memory.
    /// </summary>
    public async Task<WfTicket> MaterializeEffectiveRequestStateBatch(WfTicket ticket,
        IReadOnlyCollection<long> targetTaskIds)
    {
        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, targetTaskIds);
        if (!plan.HasChanges)
        {
            return ticket;
        }

        ValidatePlanForPersistence(plan);
        List<Dictionary<string, object?>> updates = BuildEffectiveSnapshotUpdates(plan.ChangedElements);
        try
        {
            List<ReturnId?>? results = await ApiConnection.SendQueryAsync<List<ReturnId?>>(
                RequestQueries.updateEffectiveRequestElementSnapshots, new { updates });
            RequireExactBatchResults(results, updates.Count);
        }
        catch (EffectiveRequestStateMaterializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure, exception);
        }

        WfTicket effectiveTicket = ApplyEffectiveRequestStatePlan(ticket, plan);
        await LogEffectiveStateChanges(ticket, effectiveTicket, plan.ChangedTaskIds,
            EffectiveStateHistoryText);
        return effectiveTicket;
    }

    /// <summary>
    /// Builds update-many entries containing only effective snapshot fields.
    /// </summary>
    private static List<Dictionary<string, object?>> BuildEffectiveSnapshotUpdates(
        IReadOnlyCollection<WfReqElement> elements)
    {
        List<Dictionary<string, object?>> updates = [];
        foreach (WfReqElement element in elements)
        {
            Dictionary<string, object?> values = BuildEffectiveRequestElementVariables(element);
            updates.Add(new Dictionary<string, object?>
            {
                ["where"] = IdPredicate(element.Id),
                ["_set"] = new Dictionary<string, object?>
                {
                    ["ip"] = values["ip"],
                    ["ip_end"] = values["ipEnd"],
                    ["port"] = values["port"],
                    ["port_end"] = values["portEnd"],
                    ["ip_proto_id"] = values["proto"],
                    ["group_name"] = values["groupName"],
                    ["name"] = values["name"],
                    ["start_time"] = values["startTime"],
                    ["end_time"] = values["endTime"]
                }
            });
        }
        return updates;
    }

    /// <summary>
    /// Persists exact ticket-local object-task links in one narrow batch.
    /// </summary>
    private async Task UpdateTicketObjectTaskReferencesInDb(
        IReadOnlyCollection<(WfReqElement Element, long ObjectTaskId)> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        List<Dictionary<string, object?>> updates = BuildObjectTaskReferenceUpdates(links);
        try
        {
            List<ReturnId?>? results = await ApiConnection.SendQueryAsync<List<ReturnId?>>(
                RequestQueries.updateRequestElementObjectTaskIds, new { updates });
            RequireExactBatchResults(results, updates.Count);
        }
        catch (EffectiveRequestStateMaterializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure, exception);
        }
    }

    /// <summary>
    /// Builds update-many entries containing only the exact object-task reference.
    /// </summary>
    private static List<Dictionary<string, object?>> BuildObjectTaskReferenceUpdates(
        IReadOnlyCollection<(WfReqElement Element, long ObjectTaskId)> links)
    {
        return links.Select(link => new Dictionary<string, object?>
        {
            ["where"] = IdPredicate(link.Element.Id),
            ["_set"] = new Dictionary<string, object?>
            {
                ["object_task_id"] = link.ObjectTaskId
            }
        }).ToList();
    }

    /// <summary>Builds the exact primary-key predicate shared by both request-element batches.</summary>
    private static Dictionary<string, object?> IdPredicate(long elementId)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = new Dictionary<string, object?> { ["_eq"] = elementId }
        };
    }

    /// <summary>
    /// Requires one exact row proof for every ordered update-many entry.
    /// </summary>
    private static void RequireExactBatchResults(IReadOnlyList<ReturnId?>? results,
        int expectedCount)
    {
        if (results == null || results.Count != expectedCount
            || results.Any(result => result == null || result.AffectedRows != 1))
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
        }
    }

    /// <summary>
    /// Applies the already-submitted plan without mutating the caller's ticket.
    /// </summary>
    private static WfTicket ApplyEffectiveRequestStatePlan(WfTicket ticket,
        EffectiveRequestStateMaterializationPlan plan)
    {
        WfTicket effectiveTicket = new(ticket) { Tasks = [.. ticket.Tasks] };
        foreach (IGrouping<long, WfReqElement> taskReplacements in plan.ChangedElements
            .GroupBy(element => element.TaskId))
        {
            int taskIndex = effectiveTicket.Tasks.FindIndex(task => task.Id == taskReplacements.Key);
            if (taskIndex < 0)
            {
                throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
            }

            WfReqTask originalTask = effectiveTicket.Tasks[taskIndex];
            WfReqTask effectiveTask = new(originalTask) { Elements = [.. originalTask.Elements] };
            foreach (WfReqElement replacement in taskReplacements)
            {
                int elementIndex = effectiveTask.Elements.FindIndex(element => element.Id == replacement.Id);
                if (elementIndex < 0)
                {
                    throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
                }
                effectiveTask.Elements[elementIndex] = new WfReqElement(replacement);
            }
            effectiveTicket.Tasks[taskIndex] = effectiveTask;
        }
        return effectiveTicket;
    }
}
