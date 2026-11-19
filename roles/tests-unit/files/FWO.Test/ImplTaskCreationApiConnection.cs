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
    public int MaterializationUpdateCount { get; private set; }
    public int ReadsAfterMaterializationStarted { get; private set; }
    public bool MismatchMaterializationUpdate { get; set; }
    public List<string> QueryLog { get; } = [];
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
            if (MaterializationUpdateCount > 0)
            {
                ReadsAfterMaterializationStarted++;
            }
            return (T)(object)storedTicket;
        }
        if (query == RequestQueries.updateRequestElement)
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
}
