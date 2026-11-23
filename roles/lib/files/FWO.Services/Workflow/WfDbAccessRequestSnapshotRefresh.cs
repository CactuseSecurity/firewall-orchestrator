using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

public partial class WfDbAccess
{
    /// <summary>
    /// Persists current values for every stale ticket-local request snapshot and then updates the ticket in memory.
    /// </summary>
    /// <param name="ticket">Complete ticket whose request snapshots are refreshed.</param>
    public async Task RefreshTicketRequestSnapshots(WfTicket ticket)
    {
        RequestSnapshotRefreshPlan plan = RequestSnapshotRefresher.CreatePlan(ticket);
        if (!plan.HasChanges)
        {
            return;
        }

        Dictionary<long, WfReqTask> previousTasks = plan.ChangedTaskIds.ToDictionary(
            taskId => taskId,
            taskId => CopyTask(ticket.Tasks.Single(task => task.Id == taskId)));
        List<Dictionary<string, object?>> updates = plan.Replacements
            .Select(CreateSnapshotUpdate)
            .ToList();

        List<ReturnId?>? results = await ApiConnection.SendQueryAsync<List<ReturnId?>>(
            RequestQueries.updateRequestElementSnapshots, new { updates });
        if (results == null || results.Count != updates.Count
            || results.Any(result => result == null || result.AffectedRows != 1))
        {
            throw InvalidSnapshotRefreshResult(plan, results);
        }

        RequestSnapshotRefresher.ApplyPlan(ticket, plan);
        foreach (long taskId in plan.ChangedTaskIds)
        {
            WfReqTask currentTask = ticket.Tasks.Single(task => task.Id == taskId);
            await LogWorkflowChange(
                new(ticket.Id, ModellingTypes.ChangeType.Update, ChangeHistoryObjectType.RequestTask, taskId),
                "Refreshed ticket-local request snapshots",
                RequestSnapshotHistorySnapshot(previousTasks[taskId]),
                RequestSnapshotHistorySnapshot(currentTask),
                ticket.Requester,
                true);
        }
    }

    /// <summary>
    /// Describes every planned element in a failed batch proof so the workflow error log identifies partial success.
    /// </summary>
    private static InvalidOperationException InvalidSnapshotRefreshResult(RequestSnapshotRefreshPlan plan,
        IReadOnlyList<ReturnId?>? results)
    {
        if (results == null)
        {
            return new InvalidOperationException(
                "The request snapshot refresh returned no result; no implementation tasks were created.");
        }

        List<string> outcomes = plan.Replacements.Select((element, index) =>
            $"{element.Id}={(index < results.Count && results[index] != null ? results[index]!.AffectedRows : "missing")}")
            .ToList();
        if (results.Count > plan.Replacements.Count)
        {
            outcomes.Add($"extra results={results.Count - plan.Replacements.Count}");
        }
        return new InvalidOperationException(
            "The request snapshot refresh did not update every planned element exactly once; "
            + $"affected rows by element: {string.Join(", ", outcomes)}. No implementation tasks were created.");
    }

    private static Dictionary<string, object?> CreateSnapshotUpdate(WfReqElement element)
    {
        return new Dictionary<string, object?>
        {
            ["where"] = new Dictionary<string, object?>
            {
                ["id"] = new Dictionary<string, object?> { ["_eq"] = element.Id }
            },
            ["_set"] = new Dictionary<string, object?>
            {
                ["ip"] = element.IpString,
                ["ip_end"] = element.IpEnd,
                ["port"] = element.Port,
                ["port_end"] = element.PortEnd,
                ["ip_proto_id"] = element.ProtoId,
                ["group_name"] = element.GroupName,
                ["name"] = element.Name,
                ["start_time"] = element.StartTime?.ToUniversalTime(),
                ["end_time"] = element.EndTime?.ToUniversalTime()
            }
        };
    }

    private static WfReqTask CopyTask(WfReqTask task)
    {
        return new WfReqTask(task)
        {
            Elements = task.Elements.Select(element => new WfReqElement(element)).ToList()
        };
    }

    private static object RequestSnapshotHistorySnapshot(WfReqTask task)
    {
        return task.Elements.Select(element => new
        {
            element.Id,
            element.IpString,
            element.IpEnd,
            element.Port,
            element.PortEnd,
            element.ProtoId,
            element.GroupName,
            element.Name,
            element.StartTime,
            element.EndTime
        });
    }
}
