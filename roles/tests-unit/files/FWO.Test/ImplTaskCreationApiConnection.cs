using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Test.Mocks;

namespace FWO.Test;

/// <summary>Persists enough of an implementation-task creation to exercise its workflow event.</summary>
internal sealed class ImplTaskCreationApiConnection(WfTicket storedTicket) : SimulatedApiConnection
{
    private long nextImplementationTaskId = 400;
    private long nextImplementationElementId = 500;

    public int InsertCount { get; private set; }
    public int SnapshotMutationCount { get; private set; }
    public bool ReturnInvalidSnapshotProof { get; set; }
    public List<string> QueryLog { get; } = [];
    public List<Dictionary<string, object?>> SnapshotUpdates { get; private set; } = [];
    public TaskCompletionSource? InsertStarted { get; set; }
    public TaskCompletionSource? ReleaseInsert { get; set; }

    public override async Task<T> SendQueryAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        QueryLog.Add(query);
        if (query == RequestQueries.getTicketById)
        {
            return (T)(object)storedTicket;
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
        if (query == RequestQueries.updateRequestElementSnapshots)
        {
            SnapshotMutationCount++;
            SnapshotUpdates = ReadSnapshotUpdates(variables);
            List<ReturnId?> results = SnapshotUpdates
                .Select((_, index) => (ReturnId?)new ReturnId
                {
                    AffectedRows = ReturnInvalidSnapshotProof && index == 0 ? 0 : 1
                })
                .ToList();
            return (T)(object)results;
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

    private static List<Dictionary<string, object?>> ReadSnapshotUpdates(object? variables)
    {
        object? updates = variables?.GetType().GetProperty("updates")?.GetValue(variables);
        return (List<Dictionary<string, object?>>)(updates
            ?? throw new InvalidOperationException("Snapshot updates missing."));
    }
}
