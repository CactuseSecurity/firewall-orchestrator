using System.Collections;
using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class WfDbAccessEffectiveRequestStateTest
{
    private static readonly List<string> kSnapshotVariableNames =
    [
        "id", "ip", "ipEnd", "port", "portEnd", "proto", "groupName", "name", "startTime", "endTime"
    ];

    [Test]
    public async Task MaterializeEffectiveRequestState_UpdatesChangedElementsThenReturnsVerifiedReadback()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket);
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        WfTicket result = await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.UpdatedElementIds, Is.EqualTo(Target(201, 202)));
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.getTicketById,
                ModellingQueries.addHistoryEntry)));
            Assert.That(apiConnection.UpdatePayloads, Has.Count.EqualTo(2));
            foreach (Dictionary<string, object?> payload in apiConnection.UpdatePayloads)
            {
                Assert.That(payload.Keys, Is.EquivalentTo(kSnapshotVariableNames));
            }
            Assert.That(ResultIp(result, 0),
                Is.EqualTo("10.0.1.10"));
            Assert.That(ResultIp(result, 1),
                Is.EqualTo("10.0.1.11"));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryValue(apiConnection.HistoryVariables[0], "objectId"), Is.EqualTo(20));
            Assert.That(HistoryValue(apiConnection.HistoryVariables[0], "changeText"),
                Is.EqualTo("Materialized effective request state"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_FirstWriteFailsBeforeCommit_DoesNotWriteHistory()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket) { FailBeforeCommitOnUpdateCall = 1 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(exception.InnerException, Is.SameAs(apiConnection.UpdateFailure));
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.getTicketById)));
            Assert.That(apiConnection.HistoryVariables, Is.Empty);
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 201), Is.EqualTo("10.0.0.10"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_SecondWriteFailsBeforeCommit_AuditsFirstAppliedTask()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket) { FailBeforeCommitOnUpdateCall = 2 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(exception.InnerException, Is.SameAs(apiConnection.UpdateFailure));
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.getTicketById,
                ModellingQueries.addHistoryEntry)));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 201), Is.EqualTo("10.0.1.10"));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 202), Is.EqualTo("10.0.0.10"));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryObjectId(apiConnection), Is.EqualTo(20));
            Assert.That(HistoryChangeText(apiConnection),
                Is.EqualTo("Partially materialized effective request state before failure"));
            Assert.That(SnapshotElementValue(HistoryOldSnapshot(apiConnection), 201, "IpString"),
                Is.EqualTo("10.0.0.10"));
            Assert.That(SnapshotElementValue(HistoryNewSnapshot(apiConnection), 201, "IpString"),
                Is.EqualTo("10.0.1.10/32"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_ResponseFailsAfterSecondCommit_AuditsBothAppliedRows()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket) { ThrowAfterCommitOnUpdateCall = 2 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(exception.InnerException, Is.SameAs(apiConnection.UpdateFailure));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 201), Is.EqualTo("10.0.1.10"));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 202), Is.EqualTo("10.0.1.11"));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryObjectId(apiConnection), Is.EqualTo(20));
            Assert.That(HistoryChangeText(apiConnection),
                Is.EqualTo("Partially materialized effective request state before failure"));
            Assert.That(SnapshotElementValue(HistoryNewSnapshot(apiConnection), 201, "IpString"),
                Is.EqualTo("10.0.1.10/32"));
            Assert.That(SnapshotElementValue(HistoryNewSnapshot(apiConnection), 202, "IpString"),
                Is.EqualTo("10.0.1.11/32"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_MultiTaskFailure_AuditsOnlyAppliedPlannedTask()
    {
        WfTicket ticket = CreateMultiTaskTicket();
        MaterializationApiConnection apiConnection = new(ticket)
        {
            FailBeforeCommitOnUpdateCall = 2,
            MutateUnplannedElementOnReadback = true
        };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
            await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20, 30)));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.HistoryVariables.Select((_, index) => HistoryObjectId(apiConnection, index)).ToList(),
                Is.EqualTo(Target(20)));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 101), Is.EqualTo("203.0.113.99"));
            Assert.That(ElementIp(apiConnection.CopyStoredTicket(), 301), Is.EqualTo("10.0.0.10"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_ReadbackStillStaleThrows()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket) { ReturnStaleReadback = true };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.ReadbackMismatch));
            Assert.That(apiConnection.QueryLog, Is.EqualTo(Queries(
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.updateEffectiveRequestElementSnapshot,
                RequestQueries.getTicketById)));
            Assert.That(apiConnection.HistoryVariables, Is.Empty);
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_ReadbackMissingChangedRowThrows()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket)
        {
            RemoveChangedElementOnReadback = true
        };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.ReadbackMismatch));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryChangeText(apiConnection),
                Is.EqualTo("Partially materialized effective request state before failure"));
        });
    }

    [Test]
    public void MaterializeEffectiveRequestState_ReadbackChangedTaskTypeThrows()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket)
        {
            ChangeTaskTypeOnReadback = true
        };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException exception = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.ReadbackMismatch));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryChangeText(apiConnection),
                Is.EqualTo("Partially materialized effective request state before failure"));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestState_RetryAfterPartialWriteUpdatesOnlyRemainingElements()
    {
        WfTicket ticket = CreateTicket();
        MaterializationApiConnection apiConnection = new(ticket) { FailBeforeCommitOnUpdateCall = 2 };
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        EffectiveRequestStateMaterializationException firstFailure = Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(
            async () => await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20)))!;
        WfTicket partiallyWrittenTicket = apiConnection.CopyStoredTicket();
        List<object> firstAttemptHistory = [.. apiConnection.HistoryVariables];
        apiConnection.PrepareRetry();

        WfTicket result = await dbAccess.MaterializeEffectiveRequestState(partiallyWrittenTicket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(firstFailure.Category, Is.EqualTo(EffectiveRequestStateFailureCategory.PersistenceFailure));
            Assert.That(firstAttemptHistory, Has.Count.EqualTo(1));
            Assert.That(HistoryValue(firstAttemptHistory[0], "objectId"), Is.EqualTo(20));
            Assert.That(HistoryValue(firstAttemptHistory[0], "changeText"),
                Is.EqualTo("Partially materialized effective request state before failure"));
            Assert.That(apiConnection.RetryUpdatedElementIds, Is.EqualTo(Target(202)));
            Assert.That(apiConnection.HistoryVariables, Has.Count.EqualTo(1));
            Assert.That(HistoryChangeText(apiConnection), Is.EqualTo("Materialized effective request state"));
            Assert.That(ResultIp(result, 0),
                Is.EqualTo("10.0.1.10"));
            Assert.That(ResultIp(result, 1),
                Is.EqualTo("10.0.1.11"));
        });
    }

    [Test]
    public async Task MaterializeEffectiveRequestState_NoChangesPerformsNoWrites()
    {
        WfTicket ticket = CreateTicket();
        CopySourceValuesIntoGroup(ticket);
        MaterializationApiConnection apiConnection = new(ticket);
        WfDbAccess dbAccess = CreateDbAccess(apiConnection);

        WfTicket result = await dbAccess.MaterializeEffectiveRequestState(ticket, Target(20));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(ticket));
            Assert.That(apiConnection.QueryLog, Is.Empty);
            Assert.That(apiConnection.HistoryVariables, Is.Empty);
        });
    }

    private static WfDbAccess CreateDbAccess(MaterializationApiConnection apiConnection)
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

    private static WfTicket CreateMultiTaskTicket()
    {
        WfTicket ticket = CreateTicket();
        ticket.Tasks.Add(new WfReqTask
        {
            Id = 30,
            TicketId = 100,
            TaskNumber = 4,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [ReferencingElement(301, 10, 30)]
        });
        ticket.UpdateCidrsInTaskElements();
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
        WfReqTask group = ticket.Tasks.Single(task => task.Id == 20);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(group.Elements[0],
            ticket.Tasks.Single(task => task.Id == 10).Elements[0]);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(group.Elements[1],
            ticket.Tasks.Single(task => task.Id == 11).Elements[0]);
    }

    private static List<long> Target(params long[] ids)
    {
        return [.. ids];
    }

    private static List<string> Queries(params string[] queries)
    {
        return [.. queries];
    }

    private static object? HistoryValue(object? variables, string propertyName)
    {
        if (variables is null)
        {
            return null;
        }
        PropertyInfo? property = variables.GetType().GetProperty(propertyName);
        return property?.GetValue(variables);
    }

    private static string? HistoryChangeText(MaterializationApiConnection apiConnection, int index = 0)
    {
        return HistoryValue(HistoryVariablesAt(apiConnection, index), "changeText")?.ToString();
    }

    private static long HistoryObjectId(MaterializationApiConnection apiConnection, int index = 0)
    {
        return Convert.ToInt64(HistoryValue(HistoryVariablesAt(apiConnection, index), "objectId"));
    }

    private static object? HistoryOldSnapshot(MaterializationApiConnection apiConnection, int index = 0)
    {
        return HistoryValue(HistoryVariablesAt(apiConnection, index), "oldData");
    }

    private static object? HistoryNewSnapshot(MaterializationApiConnection apiConnection, int index = 0)
    {
        return HistoryValue(HistoryVariablesAt(apiConnection, index), "newData");
    }

    private static object? HistoryVariablesAt(MaterializationApiConnection apiConnection, int index)
    {
        return index < apiConnection.HistoryVariables.Count ? apiConnection.HistoryVariables[index] : null;
    }

    private static object? SnapshotElementValue(object? snapshot, long elementId, string propertyName)
    {
        if (snapshot is null)
        {
            return null;
        }
        PropertyInfo? elementsProperty = snapshot?.GetType().GetProperty("Elements");
        IEnumerable? elements = elementsProperty?.GetValue(snapshot) as IEnumerable;
        object? element = elements?.Cast<object>().SingleOrDefault(candidate =>
            Convert.ToInt64(candidate.GetType().GetProperty("Id")?.GetValue(candidate)) == elementId);
        return element?.GetType().GetProperty(propertyName)?.GetValue(element);
    }

    private static string? ElementIp(WfTicket ticket, long elementId)
    {
        return ticket.Tasks.SelectMany(task => task.Elements).Single(element => element.Id == elementId)
            .IpString?.StripOffUnnecessaryNetmask();
    }

    private static string? ResultIp(WfTicket ticket, int elementIndex)
    {
        return ticket.Tasks.Single(task => task.Id == 20).Elements[elementIndex]
            .IpString?.StripOffUnnecessaryNetmask();
    }

    private sealed class MaterializationApiConnection : SimulatedApiConnection
    {
        private readonly WfTicket originalTicket;
        private WfTicket storedTicket;
        private int updateCallCount;
        private bool retrying;

        internal List<string> QueryLog { get; } = [];
        internal List<long> UpdatedElementIds { get; } = [];
        internal List<long> RetryUpdatedElementIds { get; } = [];
        internal List<Dictionary<string, object?>> UpdatePayloads { get; } = [];
        internal List<object> HistoryVariables { get; } = [];
        internal List<object> AlertVariables { get; } = [];
        internal int? FailBeforeCommitOnUpdateCall { get; set; }
        internal int? MismatchAfterCommitOnUpdateCall { get; set; }
        internal int? ThrowAfterCommitOnUpdateCall { get; set; }
        internal bool FailStrictReadback { get; set; }
        internal bool FailHistoryInsertion { get; set; }
        internal bool FailAlertInsertion { get; set; }
        internal bool ReturnStaleReadback { get; set; }
        internal bool RemoveChangedElementOnReadback { get; set; }
        internal bool ChangeTaskTypeOnReadback { get; set; }
        internal bool MutateUnplannedElementOnReadback { get; set; }
        internal Exception UpdateFailure { get; } = new InvalidOperationException("Simulated materialization update failure.");
        internal Exception ReadbackFailure { get; } = new InvalidOperationException("Simulated strict readback failure.");
        internal Exception HistoryFailure { get; } = new InvalidOperationException("Simulated history insertion failure.");

        internal MaterializationApiConnection(WfTicket ticket)
        {
            originalTicket = CopyTicket(ticket);
            storedTicket = CopyTicket(ticket);
        }

        internal WfTicket CopyStoredTicket()
        {
            return CopyTicket(storedTicket);
        }

        internal void PrepareRetry()
        {
            retrying = true;
            updateCallCount = 0;
            FailBeforeCommitOnUpdateCall = null;
            MismatchAfterCommitOnUpdateCall = null;
            ThrowAfterCommitOnUpdateCall = null;
            QueryLog.Clear();
            HistoryVariables.Clear();
            AlertVariables.Clear();
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query,
            object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            QueryLog.Add(query);
            if (query == RequestQueries.updateEffectiveRequestElementSnapshot)
            {
                return Task.FromResult(Cast<QueryResponseType>(UpdateElement(variables)));
            }
            if (query == RequestQueries.getTicketById)
            {
                if (FailStrictReadback)
                {
                    throw ReadbackFailure;
                }
                if (RemoveChangedElementOnReadback)
                {
                    storedTicket.Tasks.Single(task => task.Id == 20).Elements
                        .RemoveAll(element => element.Id == 201);
                }
                if (ChangeTaskTypeOnReadback)
                {
                    storedTicket.Tasks.Single(task => task.Id == 20).TaskType = WfTaskType.rule_modify.ToString();
                }
                if (MutateUnplannedElementOnReadback)
                {
                    WfReqElement unrelated = storedTicket.Tasks.Single(task => task.Id == 10).Elements.Single();
                    unrelated.IpString = "203.0.113.99";
                    unrelated.Name = "concurrent-unplanned-change";
                    storedTicket.UpdateCidrsInTaskElements();
                }
                WfTicket ticket = ReturnStaleReadback ? CopyTicket(originalTicket) : CopyTicket(storedTicket);
                return Task.FromResult(Cast<QueryResponseType>(ticket));
            }
            if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
            {
                HistoryVariables.Add(variables!);
                if (FailHistoryInsertion)
                {
                    throw HistoryFailure;
                }
                return Task.FromResult(Cast<QueryResponseType>(new ReturnIdWrapper()));
            }
            if (query == MonitorQueries.getOpenAlerts)
            {
                return Task.FromResult(Cast<QueryResponseType>(new List<Alert>()));
            }
            if (query == MonitorQueries.addAlert)
            {
                AlertVariables.Add(variables!);
                if (FailAlertInsertion)
                {
                    throw new InvalidOperationException("Simulated alert insertion failure.");
                }
                return Task.FromResult(Cast<QueryResponseType>(new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { NewIdLong = 1 }]
                }));
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private ReturnId UpdateElement(object? variables)
        {
            Dictionary<string, object?> payload = (Dictionary<string, object?>)variables!;
            UpdatePayloads.Add(new Dictionary<string, object?>(payload));
            long elementId = Convert.ToInt64(payload["id"]);
            updateCallCount++;
            UpdatedElementIds.Add(elementId);
            if (retrying)
            {
                RetryUpdatedElementIds.Add(elementId);
            }
            if (FailBeforeCommitOnUpdateCall == updateCallCount)
            {
                throw UpdateFailure;
            }

            WfReqElement element = storedTicket.Tasks.SelectMany(task => task.Elements)
                .Single(candidate => candidate.Id == elementId);
            element.IpString = payload["ip"]?.ToString();
            element.IpEnd = payload["ipEnd"]?.ToString();
            element.Name = payload["name"]?.ToString();
            element.GroupName = payload["groupName"]?.ToString();
            element.Port = payload["port"] as int?;
            element.PortEnd = payload["portEnd"] as int?;
            element.ProtoId = payload["proto"] as int?;
            storedTicket.UpdateCidrsInTaskElements();
            if (ThrowAfterCommitOnUpdateCall == updateCallCount)
            {
                throw UpdateFailure;
            }
            if (MismatchAfterCommitOnUpdateCall == updateCallCount)
            {
                return new ReturnId { UpdatedIdLong = elementId + 1 };
            }
            return new ReturnId { UpdatedIdLong = elementId };
        }

        private static QueryResponseType Cast<QueryResponseType>(object value)
        {
            return (QueryResponseType)value;
        }

        private static WfTicket CopyTicket(WfTicket ticket)
        {
            WfTicket copy = new(ticket)
            {
                Tasks = ticket.Tasks.Select(CopyTask).ToList()
            };
            copy.UpdateCidrsInTaskElements();
            return copy;
        }

        private static WfReqTask CopyTask(WfReqTask task)
        {
            return new WfReqTask(task)
            {
                Elements = task.Elements.Select(element => new WfReqElement(element)).ToList()
            };
        }
    }
}
