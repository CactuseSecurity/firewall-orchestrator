using System.Collections;
using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class WfDbAccessEffectiveRequestStateBatchTest
{
    private static readonly List<string> kSnapshotColumns =
    [
        "ip", "ip_end", "port", "port_end", "ip_proto_id", "group_name", "name", "start_time", "end_time"
    ];
    private static readonly List<string> kUpdateKeys = ["where", "_set"];

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_WritesAllChangesOnceWithoutReadback()
    {
        WfTicket ticket = CreateTicket();
        BatchApiConnection apiConnection = new();
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        WfTicket result = await dbAccess.MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshots,
                ModellingQueries.addHistoryEntry)));
            Assert.That(apiConnection.Updates, Has.Count.EqualTo(2));
            Assert.That(ElementIp(result, 201), Is.EqualTo("10.0.1.10"));
            Assert.That(ElementIp(result, 202), Is.EqualTo("10.0.1.11"));
            Assert.That(ElementIp(ticket, 201), Is.EqualTo("10.0.0.10"));
            Assert.That(ElementIp(ticket, 202), Is.EqualTo("10.0.0.10"));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_UsesExactSnapshotPayload()
    {
        WfTicket ticket = CreateTicket();
        WfReqElement source = ticket.Tasks.Single(task => task.Id == 10).Elements.Single();
        source.IpString = "198.51.100.10/32";
        source.IpEnd = "198.51.100.20/32";
        source.Port = 443;
        source.PortEnd = 444;
        source.ProtoId = 6;
        source.GroupName = "current-group";
        source.Name = "current-name";
        source.StartTime = new DateTime(2026, 11, 20, 8, 30, 0, DateTimeKind.Local);
        source.EndTime = new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Local);
        ticket.UpdateCidrsInTaskElements();
        BatchApiConnection apiConnection = new();
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        await dbAccess.MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Dictionary<string, object?> firstUpdate = (Dictionary<string, object?>)apiConnection.Updates[0];
        Dictionary<string, object?> set = (Dictionary<string, object?>)firstUpdate["_set"]!;
        Assert.Multiple(() =>
        {
            Assert.That(firstUpdate.Keys, Is.EquivalentTo(kUpdateKeys));
            Assert.That(ReadUpdateId(firstUpdate), Is.EqualTo(201));
            Assert.That(set.Keys, Is.EquivalentTo(kSnapshotColumns));
            Assert.That(set["ip"], Is.EqualTo("198.51.100.10/32"));
            Assert.That(set["ip_end"], Is.EqualTo("198.51.100.20/32"));
            Assert.That(set["port"], Is.EqualTo(443));
            Assert.That(set["port_end"], Is.EqualTo(444));
            Assert.That(set["ip_proto_id"], Is.EqualTo(6));
            Assert.That(set["group_name"], Is.EqualTo("current-group"));
            Assert.That(set["name"], Is.EqualTo("current-name"));
            Assert.That(set["start_time"], Is.EqualTo(source.StartTime.Value.ToUniversalTime()));
            Assert.That(set["end_time"], Is.EqualTo(source.EndTime.Value.ToUniversalTime()));
        });
    }

    [TestCase(BatchResultShape.NullResponse)]
    [TestCase(BatchResultShape.WrongCount)]
    [TestCase(BatchResultShape.NullEntry)]
    [TestCase(BatchResultShape.ZeroRows)]
    [TestCase(BatchResultShape.MultipleRows)]
    public void MaterializeEffectiveRequestStateBatch_RejectsInvalidBatchProof(BatchResultShape shape)
    {
        WfTicket ticket = CreateTicket();
        BatchApiConnection apiConnection = new() { ResultShape = shape };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception =
            Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
                await dbAccess.MaterializeEffectiveRequestStateBatch(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category,
                Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(apiConnection.QueryLog,
                Is.EqualTo(Queries(RequestQueries.updateEffectiveRequestElementSnapshots)));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestStateBatch_WrapsTransportFailureWithoutReadback()
    {
        WfTicket ticket = CreateTicket();
        BatchApiConnection apiConnection = new() { ThrowBatchFailure = true };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception =
            Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
                await dbAccess.MaterializeEffectiveRequestStateBatch(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category,
                Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(exception.InnerException, Is.SameAs(apiConnection.BatchFailure));
            Assert.That(apiConnection.QueryLog,
                Is.EqualTo(Queries(RequestQueries.updateEffectiveRequestElementSnapshots)));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_ClearsMissingServiceSnapshotValues()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        group.TaskNumber = 4;
        WfReqTask serviceTask = new()
        {
            Id = 12,
            TicketId = ticket.Id,
            TaskNumber = 3,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = 121,
                    TaskId = 12,
                    Field = ElemFieldType.service.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Port = 0,
                    PortEnd = 0,
                    ProtoId = 0
                }
            ]
        };
        group.Elements.Add(new WfReqElement
        {
            Id = 204,
            TaskId = 20,
            Field = ElemFieldType.service.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ObjectTaskId = 12,
            ServiceId = 77,
            Port = 443,
            PortEnd = 444,
            ProtoId = 6,
            GroupName = "stale-group",
            Name = "stale-name",
            StartTime = new DateTime(2026, 11, 20, 8, 30, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc)
        });
        ticket.Tasks.Insert(2, serviceTask);
        BatchApiConnection apiConnection = new();

        await CreateDbAccess(apiConnection).MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Dictionary<string, object?> serviceUpdate = apiConnection.Updates
            .Cast<Dictionary<string, object?>>()
            .Single(update => ReadUpdateId(update) == 204);
        Dictionary<string, object?> set = (Dictionary<string, object?>)serviceUpdate["_set"]!;
        Assert.Multiple(() =>
        {
            Assert.That(set["port"], Is.Null);
            Assert.That(set["port_end"], Is.Null);
            Assert.That(set["ip_proto_id"], Is.Null);
            Assert.That(set["group_name"], Is.Null);
            Assert.That(set["name"], Is.Null);
            Assert.That(set["start_time"], Is.Null);
            Assert.That(set["end_time"], Is.Null);
        });
    }

    [TestCase(InvalidElementShape.NonPositive)]
    [TestCase(InvalidElementShape.Duplicate)]
    public void MaterializeEffectiveRequestStateBatch_RejectsInvalidElementIdsBeforeWrite(
        InvalidElementShape shape)
    {
        WfTicket ticket = CreateTicket();
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        if (shape == InvalidElementShape.NonPositive)
        {
            group.Elements[0].Id = 0;
        }
        else
        {
            group.Elements[1].Id = group.Elements[0].Id;
        }
        BatchApiConnection apiConnection = new();

        Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
            await CreateDbAccess(apiConnection).MaterializeEffectiveRequestStateBatch(ticket, Target(20)));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.QueryLog, Is.Empty);
            Assert.That(apiConnection.Updates, Is.Empty);
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_PreservesUnchangedInstancesAndOrder()
    {
        WfTicket ticket = CreateTicket();
        List<WfReqTask> originalTasks = [.. ticket.Tasks];
        WfReqTask originalGroup = ticket.Tasks.Single(task => task.Id == 20);
        WfReqElement stableCatalogMember = originalGroup.Elements.Single(element => element.Id == 203);

        WfTicket result = await CreateDbAccess(new BatchApiConnection())
            .MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        WfReqTask resultGroup = result.Tasks.Single(task => task.Id == 20);
        Assert.Multiple(() =>
        {
            Assert.That(result.Tasks.Select(task => task.Id),
                Is.EqualTo(originalTasks.Select(task => task.Id)));
            Assert.That(result.Tasks[0], Is.SameAs(originalTasks[0]));
            Assert.That(resultGroup, Is.Not.SameAs(originalGroup));
            Assert.That(resultGroup.Elements.Single(element => element.Id == 203),
                Is.SameAs(stableCatalogMember));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_CurrentTicketPerformsNoCalls()
    {
        WfTicket ticket = CreateTicket();
        CopySourceValuesIntoGroup(ticket);
        BatchApiConnection apiConnection = new();

        WfTicket result = await CreateDbAccess(apiConnection)
            .MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(ticket));
            Assert.That(apiConnection.QueryLog, Is.Empty);
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_HasNoProductDefinedUpdateCountLimit()
    {
        const int updateCount = 1025;
        WfTicket ticket = CreateTicket();
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        group.Elements = Enumerable.Range(1, updateCount)
            .Select(index => ReferencingElement(1000 + index, 10))
            .ToList();
        BatchApiConnection apiConnection = new();

        await CreateDbAccess(apiConnection)
            .MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Assert.That(apiConnection.Updates, Has.Count.EqualTo(updateCount));
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_InvalidMixedProofCanRetryOnlyStaleRow()
    {
        WfTicket ticket = CreateThreeReferenceTicket();
        BatchApiConnection failedConnection = new() { ResultShape = BatchResultShape.MiddleZero };

        Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
            await CreateDbAccess(failedConnection)
                .MaterializeEffectiveRequestStateBatch(ticket, Target(20)));

        WfTicket partiallyCurrent = CopyTicket(ticket);
        CopyReferenceSource(partiallyCurrent, 201, 10);
        CopyReferenceSource(partiallyCurrent, 204, 12);
        BatchApiConnection retryConnection = new();
        WfTicket result = await CreateDbAccess(retryConnection)
            .MaterializeEffectiveRequestStateBatch(partiallyCurrent, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(failedConnection.Updates, Has.Count.EqualTo(3));
            Assert.That(retryConnection.Updates.Cast<Dictionary<string, object?>>()
                .Select(ReadUpdateId), Is.EqualTo(Target(202)));
            Assert.That(ElementIp(result, 202), Is.EqualTo("10.0.1.11"));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_HistoryFailureDoesNotReadBack()
    {
        WfTicket ticket = CreateTicket();
        BatchApiConnection apiConnection = new() { ThrowHistoryFailure = true };

        WfTicket result = await CreateDbAccess(apiConnection)
            .MaterializeEffectiveRequestStateBatch(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(ElementIp(result, 201), Is.EqualTo("10.0.1.10"));
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshots,
                ModellingQueries.addHistoryEntry)));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestStateBatch_RecordsOneHistoryPerChangedTask()
    {
        WfTicket ticket = CreateTicket();
        ticket.Tasks.Add(new WfReqTask
        {
            Id = 30,
            TicketId = ticket.Id,
            TaskNumber = 4,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [ReferencingElement(301, 10, 30)]
        });
        BatchApiConnection apiConnection = new();

        await CreateDbAccess(apiConnection)
            .MaterializeEffectiveRequestStateBatch(ticket, Target(20, 30));

        Assert.That(apiConnection.HistoryVariables.Select(HistoryObjectId),
            Is.EqualTo(Target(20, 30)));
    }

    private static WfDbAccess CreateDbAccess(BatchApiConnection apiConnection)
    {
        UserConfig userConfig = new();
        ActionHandler actionHandler = new(apiConnection, new WfHandler());
        return new WfDbAccess(DefaultInit.DoNothing, userConfig, apiConnection, actionHandler,
            false, WorkflowPhases.request);
    }

    private static WfTicket CreateTicket()
    {
        WfReqTask firstObject = ObjectTask(10, 1, "server-a", "10.0.1.10");
        WfReqTask secondObject = ObjectTask(11, 2, "server-b", "10.0.1.11");
        WfReqTask group = new()
        {
            Id = 20,
            TicketId = 100,
            TaskNumber = 3,
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                ReferencingElement(201, 10),
                ReferencingElement(202, 11),
                new WfReqElement
                {
                    Id = 203,
                    TaskId = 20,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.unchanged.ToString(),
                    NetworkId = 77,
                    Name = "catalog",
                    IpString = "192.0.2.10"
                }
            ]
        };
        WfTicket ticket = new() { Id = 100, Tasks = [firstObject, secondObject, group] };
        ticket.UpdateCidrsInTaskElements();
        return ticket;
    }

    private static WfTicket CreateThreeReferenceTicket()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        group.TaskNumber = 4;
        ticket.Tasks.Insert(2, ObjectTask(12, 3, "server-c", "10.0.1.12"));
        group.Elements.Add(ReferencingElement(204, 12));
        return ticket;
    }

    private static WfReqTask ObjectTask(long id, int taskNumber, string name, string ip)
    {
        return new WfReqTask
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = id * 10 + 1,
                    TaskId = id,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = name,
                    IpString = ip
                }
            ]
        };
    }

    private static WfReqElement ReferencingElement(long id, long objectTaskId, long taskId = 20)
    {
        return new WfReqElement
        {
            Id = id,
            TaskId = taskId,
            Field = ElemFieldType.source.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ObjectTaskId = objectTaskId,
            Name = "stale",
            IpString = "10.0.0.10"
        };
    }

    private static void CopySourceValuesIntoGroup(WfTicket ticket)
    {
        CopyReferenceSource(ticket, 201, 10);
        CopyReferenceSource(ticket, 202, 11);
    }

    private static void CopyReferenceSource(WfTicket ticket, long elementId, long sourceTaskId)
    {
        WfReqElement target = ticket.Tasks.SelectMany(task => task.Elements)
            .Single(element => element.Id == elementId);
        WfReqElement source = ticket.Tasks.Single(task => task.Id == sourceTaskId).Elements.Single();
        WfObjectTaskHelper.CopyRequestedStateSnapshot(target, source);
    }

    private static WfTicket CopyTicket(WfTicket ticket)
    {
        return new WfTicket(ticket)
        {
            Tasks = ticket.Tasks.Select(task => new WfReqTask(task)
            {
                Elements = task.Elements.Select(element => new WfReqElement(element)).ToList()
            }).ToList()
        };
    }

    private static string? ElementIp(WfTicket ticket, long elementId)
    {
        return ticket.Tasks.SelectMany(task => task.Elements)
            .Single(element => element.Id == elementId)
            .IpString?.StripOffUnnecessaryNetmask();
    }

    private static long ReadUpdateId(Dictionary<string, object?> update)
    {
        Dictionary<string, object?> where = (Dictionary<string, object?>)update["where"]!;
        Dictionary<string, object?> id = (Dictionary<string, object?>)where["id"]!;
        return Convert.ToInt64(id["_eq"]);
    }

    private static long HistoryObjectId(object variables)
    {
        return Convert.ToInt64(ReadProperty<object>(variables, "objectId"));
    }

    private static T ReadProperty<T>(object? value, string propertyName)
    {
        PropertyInfo? property = value?.GetType().GetProperty(propertyName);
        return property?.GetValue(value) is T result
            ? result
            : throw new InvalidOperationException($"Missing property '{propertyName}'.");
    }

    private static List<long> Target(params long[] ids)
    {
        return [.. ids];
    }

    private static List<string> Queries(params string[] queries)
    {
        return [.. queries];
    }

    private sealed class BatchApiConnection : SimulatedApiConnection
    {
        internal List<string> QueryLog { get; } = [];
        internal List<object> Updates { get; } = [];
        internal List<object> HistoryVariables { get; } = [];
        internal BatchResultShape ResultShape { get; set; } = BatchResultShape.Success;
        internal bool ThrowBatchFailure { get; set; }
        internal bool ThrowHistoryFailure { get; set; }
        internal Exception BatchFailure { get; } = new InvalidOperationException("Simulated batch transport failure.");

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query,
            object? variables = null, string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            QueryLog.Add(query);
            if (query == RequestQueries.updateEffectiveRequestElementSnapshots)
            {
                List<object> updates = ReadProperty<IEnumerable>(variables, "updates")
                    .Cast<object>()
                    .ToList();
                Updates.AddRange(updates);
                if (ThrowBatchFailure)
                {
                    throw BatchFailure;
                }
                return Task.FromResult(Cast<QueryResponseType>(BatchResults(updates.Count)));
            }
            if (query == ModellingQueries.addHistoryEntry
                || query == ModellingQueries.addHistoryEntryAsService)
            {
                HistoryVariables.Add(variables!);
                if (ThrowHistoryFailure)
                {
                    throw new InvalidOperationException("Simulated history failure.");
                }
                return Task.FromResult(Cast<QueryResponseType>(new ReturnIdWrapper()));
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private List<ReturnId?>? BatchResults(int updateCount)
        {
            List<ReturnId?> success = Enumerable.Range(0, updateCount)
                .Select(_ => (ReturnId?)new ReturnId { AffectedRows = 1 })
                .ToList();
            return ResultShape switch
            {
                BatchResultShape.NullResponse => null,
                BatchResultShape.WrongCount => success.Take(Math.Max(0, updateCount - 1)).ToList(),
                BatchResultShape.NullEntry => ReplaceFirst(success, null),
                BatchResultShape.ZeroRows => ReplaceFirst(success, new ReturnId { AffectedRows = 0 }),
                BatchResultShape.MultipleRows => ReplaceFirst(success, new ReturnId { AffectedRows = 2 }),
                BatchResultShape.MiddleZero => ReplaceMiddle(success),
                _ => success
            };
        }

        private static List<ReturnId?> ReplaceFirst(List<ReturnId?> results, ReturnId? replacement)
        {
            results[0] = replacement;
            return results;
        }

        private static List<ReturnId?> ReplaceMiddle(List<ReturnId?> results)
        {
            results[1] = new ReturnId { AffectedRows = 0 };
            return results;
        }

        private static QueryResponseType Cast<QueryResponseType>(object? value)
        {
            return (QueryResponseType)value!;
        }
    }

    public enum BatchResultShape
    {
        Success,
        NullResponse,
        WrongCount,
        NullEntry,
        ZeroRows,
        MultipleRows,
        MiddleZero
    }

    public enum InvalidElementShape
    {
        NonPositive,
        Duplicate
    }
}
