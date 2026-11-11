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

    public override Task<T> SendQueryAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        if (query == RequestQueries.getTicketById)
        {
            return Task.FromResult((T)(object)storedTicket);
        }
        if (query == RequestQueries.newImplementationTask)
        {
            InsertCount++;
            return Task.FromResult((T)(object)new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewIdLong = ++nextImplementationTaskId }]
            });
        }
        if (query == RequestQueries.newImplementationElement)
        {
            return Task.FromResult((T)(object)new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { NewIdLong = ++nextImplementationElementId }]
            });
        }
        if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
        {
            return Task.FromResult((T)(object)new ReturnIdWrapper());
        }
        if (query == RequestQueries.recordStateChangeExecution)
        {
            return Task.FromResult((T)(object)new ReturnId { AffectedRows = 1 });
        }
        throw new InvalidOperationException($"Unexpected implementation-task creation query: {query}");
    }
}
