using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Test.Mocks;
using System.Collections;

namespace FWO.Test;

/// <summary>Persists enough of an implementation-task creation to exercise its workflow event.</summary>
internal sealed class ImplTaskCreationApiConnection(WfTicket storedTicket) : SimulatedApiConnection
{
    private long nextImplementationTaskId = 400;
    private long nextImplementationElementId = 500;

    public int InsertCount { get; private set; }
    public int MaterializationUpdateCount { get; private set; }
    public int MaterializationBatchCount { get; private set; }
    public int TicketReadCount { get; private set; }
    public int ReadsAfterMaterializationStarted { get; private set; }
    public bool MismatchMaterializationUpdate { get; set; }
    public List<string> QueryLog { get; } = [];
    public List<long> MaterializedElementIds { get; } = [];
    public TaskCompletionSource? InsertStarted { get; set; }
    public TaskCompletionSource? ReleaseInsert { get; set; }
    public List<Device> PathDevices { get; } = [];

    public override async Task<T> SendQueryAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        QueryLog.Add(query);
        if (query == PathAnalysisQueries.pathAnalysis)
        {
            return (T)(object)PathDevices;
        }
        if (query == RequestQueries.getTicketById)
        {
            TicketReadCount++;
            if (MaterializationUpdateCount > 0)
            {
                ReadsAfterMaterializationStarted++;
            }
            return (T)(object)storedTicket;
        }
        if (query == RequestQueries.updateEffectiveRequestElementSnapshot)
        {
            Dictionary<string, object?> payload = (Dictionary<string, object?>)variables!;
            long elementId = Convert.ToInt64(payload["id"]);
            MaterializationUpdateCount++;
            if (!MismatchMaterializationUpdate)
            {
                WfReqElement element = storedTicket.Tasks.SelectMany(task => task.Elements)
                    .Single(candidate => candidate.Id == elementId);
                element.IpString = payload["ip"]?.ToString();
                element.Name = payload["name"]?.ToString();
                storedTicket.UpdateCidrsInTaskElements();
            }
            long returnedId = MismatchMaterializationUpdate ? elementId + 1 : elementId;
            return (T)(object)new ReturnId { UpdatedIdLong = returnedId };
        }
        if (query == RequestQueries.updateEffectiveRequestElementSnapshots)
        {
            MaterializationBatchCount++;
            IEnumerable rawUpdates = (IEnumerable)(variables?.GetType().GetProperty("updates")?
                .GetValue(variables) ?? Array.Empty<object>());
            List<Dictionary<string, object?>> updates = rawUpdates
                .Cast<Dictionary<string, object?>>()
                .ToList();
            foreach (Dictionary<string, object?> update in updates)
            {
                ApplySnapshotUpdate(update);
            }
            MaterializationUpdateCount += updates.Count;
            List<ReturnId?> results = updates.Select(_ => (ReturnId?)new ReturnId
            {
                AffectedRows = MismatchMaterializationUpdate ? 0 : 1
            }).ToList();
            return (T)(object)results;
        }
        if (query == RequestQueries.newImplementationTask)
        {
            InsertCount++;
            InsertStarted?.TrySetResult();
            if (ReleaseInsert != null)
            {
                await ReleaseInsert.Task;
            }
            return (T)(object)new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewIdLong = ++nextImplementationTaskId }]
            };
        }
        if (query == RequestQueries.newImplementationElement)
        {
            return (T)(object)new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewIdLong = ++nextImplementationElementId }]
            };
        }
        if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
        {
            return (T)(object)new ReturnIdWrapper();
        }
        if (query == RequestQueries.recordStateChangeExecution)
        {
            return (T)(object)new ReturnId { AffectedRows = 1 };
        }
        throw new InvalidOperationException($"Unexpected implementation-task creation query: {query}");
    }

    private void ApplySnapshotUpdate(Dictionary<string, object?> update)
    {
        Dictionary<string, object?> where = (Dictionary<string, object?>)update["where"]!;
        Dictionary<string, object?> id = (Dictionary<string, object?>)where["id"]!;
        long elementId = Convert.ToInt64(id["_eq"]);
        MaterializedElementIds.Add(elementId);
        Dictionary<string, object?> set = (Dictionary<string, object?>)update["_set"]!;
        WfReqElement element = storedTicket.Tasks.SelectMany(task => task.Elements)
            .Single(candidate => candidate.Id == elementId);
        element.IpString = set["ip"]?.ToString();
        element.IpEnd = set["ip_end"]?.ToString();
        element.Port = set["port"] as int?;
        element.PortEnd = set["port_end"] as int?;
        element.ProtoId = set["ip_proto_id"] as int?;
        element.GroupName = set["group_name"]?.ToString();
        element.Name = set["name"]?.ToString();
        element.StartTime = set["start_time"] as DateTime?;
        element.EndTime = set["end_time"] as DateTime?;
        storedTicket.UpdateCidrsInTaskElements();
    }
}
