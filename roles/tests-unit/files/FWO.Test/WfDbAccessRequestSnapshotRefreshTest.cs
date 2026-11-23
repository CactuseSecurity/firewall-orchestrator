using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class WfDbAccessRequestSnapshotRefreshTest
{
    private static readonly List<string> kUpdateKeys = ["where", "_set"];
    private static readonly List<long> kChangedTaskIds = [20, 30];
    private static readonly List<string> kSnapshotColumns =
    [
        "ip", "ip_end", "port", "port_end", "ip_proto_id", "group_name", "name", "start_time", "end_time"
    ];

    [Test]
    public async Task RefreshTicketRequestSnapshots_WritesOneNarrowBatchThenUpdatesMemoryAndHistory()
    {
        WfTicket ticket = CreateTicket();
        RecordingApiConnection apiConnection = new();

        await CreateDbAccess(apiConnection).RefreshTicketRequestSnapshots(ticket);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.QueryLog.First(), Is.EqualTo(RequestQueries.updateRequestElementSnapshots));
            Assert.That(apiConnection.Updates, Has.Count.EqualTo(2));
            Assert.That(apiConnection.Updates.All(update => update.Keys.SequenceEqual(kUpdateKeys)), Is.True);
            Assert.That(apiConnection.Updates.All(update => SetValues(update).Keys.SequenceEqual(kSnapshotColumns)), Is.True);
            Assert.That(UpdateId(apiConnection.Updates[0]), Is.EqualTo(201));
            Assert.That(UpdateId(apiConnection.Updates[1]), Is.EqualTo(301));
            Assert.That(SetValues(apiConnection.Updates[0])["ip"], Is.EqualTo("10.0.1.10/32"));
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(2));
            Assert.That(apiConnection.HistoryVariables.Select(HistoryObjectId), Is.EqualTo(kChangedTaskIds));
        });
    }

    [Test]
    public async Task RefreshTicketRequestSnapshots_CurrentTicketPerformsNoCalls()
    {
        WfTicket ticket = CreateTicket();
        CopyCurrentValues(ticket);
        RecordingApiConnection apiConnection = new();

        await CreateDbAccess(apiConnection).RefreshTicketRequestSnapshots(ticket);

        Assert.That(apiConnection.QueryLog, Is.Empty);
    }

    [TestCase(BatchResultShape.NullResponse)]
    [TestCase(BatchResultShape.WrongCount)]
    [TestCase(BatchResultShape.NullEntry)]
    [TestCase(BatchResultShape.ZeroRows)]
    [TestCase(BatchResultShape.MultipleRows)]
    public void RefreshTicketRequestSnapshots_InvalidBatchProofLeavesMemoryUnchanged(BatchResultShape resultShape)
    {
        WfTicket ticket = CreateTicket();
        RecordingApiConnection apiConnection = new() { ResultShape = resultShape };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateDbAccess(apiConnection).RefreshTicketRequestSnapshots(ticket));

        Assert.Multiple(() =>
        {
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(apiConnection.HistoryVariables, Is.Empty);
        });
    }

    [Test]
    public void RefreshTicketRequestSnapshots_TransportFailureLeavesMemoryUnchanged()
    {
        WfTicket ticket = CreateTicket();
        RecordingApiConnection apiConnection = new() { ThrowBatchFailure = true };

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateDbAccess(apiConnection).RefreshTicketRequestSnapshots(ticket))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.SameAs(apiConnection.BatchFailure));
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(apiConnection.HistoryVariables, Is.Empty);
        });
    }

    [Test]
    public async Task RefreshTicketRequestSnapshots_HistoryFailureKeepsSuccessfulRefresh()
    {
        WfTicket ticket = CreateTicket();
        RecordingApiConnection apiConnection = new() { ThrowHistoryFailure = true };

        await CreateDbAccess(apiConnection).RefreshTicketRequestSnapshots(ticket);

        Assert.Multiple(() =>
        {
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(2));
        });
    }

    private static WfDbAccess CreateDbAccess(RecordingApiConnection apiConnection)
    {
        UserConfig userConfig = new();
        WfHandler handler = new() { userConfig = userConfig };
        ActionHandler actionHandler = new(apiConnection, handler);
        return new WfDbAccess(DefaultInit.DoNothing, userConfig, apiConnection, actionHandler,
            false, WorkflowPhases.request, false);
    }

    private static WfTicket CreateTicket()
    {
        WfReqTask objectTask = new()
        {
            Id = 10,
            TicketId = 100,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = 101,
                    TaskId = 10,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = "current",
                    IpString = "10.0.1.10/32"
                }
            ]
        };
        WfTicket ticket = new()
        {
            Id = 100,
            Requester = new UiUser { DbId = 7 },
            Tasks =
            [
                objectTask,
                ConsumerTask(20, 201, ElemFieldType.source),
                ConsumerTask(30, 301, ElemFieldType.destination)
            ]
        };
        ticket.UpdateCidrsInTaskElements();
        return ticket;
    }

    private static WfReqTask ConsumerTask(long taskId, long elementId, ElemFieldType field)
    {
        return new WfReqTask
        {
            Id = taskId,
            TicketId = 100,
            TaskType = taskId == 20 ? WfTaskType.group_create.ToString() : WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = elementId,
                    TaskId = taskId,
                    ObjectTaskId = 10,
                    Field = field.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = "stale",
                    IpString = "10.0.0.10/32"
                }
            ]
        };
    }

    private static void CopyCurrentValues(WfTicket ticket)
    {
        WfReqElement source = Element(ticket, 101);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(Element(ticket, 201), source);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(Element(ticket, 301), source);
    }

    private static WfReqElement Element(WfTicket ticket, long elementId)
    {
        return ticket.Tasks.SelectMany(task => task.Elements).Single(element => element.Id == elementId);
    }

    private static Dictionary<string, object?> SetValues(Dictionary<string, object?> update)
    {
        return (Dictionary<string, object?>)update["_set"]!;
    }

    private static long UpdateId(Dictionary<string, object?> update)
    {
        Dictionary<string, object?> where = (Dictionary<string, object?>)update["where"]!;
        Dictionary<string, object?> id = (Dictionary<string, object?>)where["id"]!;
        return (long)id["_eq"]!;
    }

    private static long HistoryObjectId(object variables)
    {
        object? value = variables.GetType().GetProperty("objectId")?.GetValue(variables);
        return Convert.ToInt64(value);
    }

    internal enum BatchResultShape
    {
        Valid,
        NullResponse,
        WrongCount,
        NullEntry,
        ZeroRows,
        MultipleRows
    }

    private sealed class RecordingApiConnection : SimulatedApiConnection
    {
        public BatchResultShape ResultShape { get; init; }
        public bool ThrowBatchFailure { get; init; }
        public bool ThrowHistoryFailure { get; init; }
        public InvalidOperationException BatchFailure { get; } = new("batch failed");
        public List<string> QueryLog { get; } = [];
        public List<Dictionary<string, object?>> Updates { get; private set; } = [];
        public List<object> HistoryVariables { get; } = [];

        public override Task<T> SendQueryAsync<T>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            QueryLog.Add(query);
            if (query == RequestQueries.updateRequestElementSnapshots)
            {
                if (ThrowBatchFailure)
                {
                    throw BatchFailure;
                }
                Updates = ReadUpdates(variables);
                return Task.FromResult((T)(object?)BatchResult(Updates.Count)!);
            }
            if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
            {
                HistoryVariables.Add(variables!);
                if (ThrowHistoryFailure)
                {
                    throw new InvalidOperationException("history failed");
                }
                return Task.FromResult((T)(object)new ReturnIdWrapper());
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private List<ReturnId?>? BatchResult(int updateCount)
        {
            List<ReturnId?> valid = Enumerable.Range(0, updateCount)
                .Select(_ => (ReturnId?)new ReturnId { AffectedRows = 1 })
                .ToList();
            return ResultShape switch
            {
                BatchResultShape.Valid => valid,
                BatchResultShape.NullResponse => null,
                BatchResultShape.WrongCount => valid.Take(Math.Max(0, updateCount - 1)).ToList(),
                BatchResultShape.NullEntry => valid.Select((result, index) => index == 0 ? null : result).ToList(),
                BatchResultShape.ZeroRows => valid.Select((result, index) => index == 0
                    ? new ReturnId { AffectedRows = 0 } : result).ToList(),
                _ => valid.Select((result, index) => index == 0
                    ? new ReturnId { AffectedRows = 2 } : result).ToList()
            };
        }

        private static List<Dictionary<string, object?>> ReadUpdates(object? variables)
        {
            object? updates = variables?.GetType().GetProperty("updates")?.GetValue(variables);
            return (List<Dictionary<string, object?>>)(updates
                ?? throw new InvalidOperationException("Updates missing."));
        }
    }
}
