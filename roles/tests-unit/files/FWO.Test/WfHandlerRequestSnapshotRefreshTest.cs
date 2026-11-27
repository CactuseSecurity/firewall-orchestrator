using System.Reflection;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class WfHandlerRequestSnapshotRefreshTest
{
    private static readonly List<long> kBothSnapshotElementIds = [201, 301];

    [Test]
    public async Task CreateMissingImplTasksForTicket_RefreshesWholeTicketBeforeFirstInsert()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask scopedTask = ticket.Tasks.Single(task => task.Id == 20);
        ImplTaskCreationApiConnection apiConnection = new(ticket);
        WfHandler handler = CreateHandler(ticket, apiConnection);

        await handler.CreateMissingImplTasksForTicket(ticket, scopedTask);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.SnapshotMutationCount, Is.EqualTo(1));
            Assert.That(apiConnection.SnapshotUpdates.Select(UpdateId), Is.EqualTo(kBothSnapshotElementIds));
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.1.10/32"));
            Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
            Assert.That(BatchIndex(apiConnection), Is.LessThan(FirstInsertIndex(apiConnection)));
        });
    }

    [Test]
    public async Task CreateMissingImplTasksForTicket_BundleAndMultipleDevicesRefreshOnce()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask firstTask = ticket.Tasks.Single(task => task.Id == 20);
        WfReqTask secondTask = ticket.Tasks.Single(task => task.Id == 30);
        firstTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-20-30");
        secondTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-20-30");
        firstTask.SetDeviceList([21]);
        secondTask.SetDeviceList([22]);
        ImplTaskCreationApiConnection apiConnection = new(ticket);
        WfHandler handler = CreateHandler(ticket, apiConnection, considerBundling: true,
            ReqDeviceInputOptions.enterInReqTask);
        handler.Devices = [new Device { Id = 21 }, new Device { Id = 22 }];

        await handler.CreateMissingImplTasksForTicket(ticket, firstTask);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.SnapshotMutationCount, Is.EqualTo(1));
            Assert.That(apiConnection.InsertCount, Is.EqualTo(2));
            Assert.That(apiConnection.QueryLog
                .Where(query => query == FWO.Api.Client.Queries.RequestQueries.newImplementationTask)
                .All(_ => BatchIndex(apiConnection) < FirstInsertIndex(apiConnection)), Is.True);
        });
    }

    [Test]
    public void CreateMissingImplTasksForTicket_RefreshFailureCreatesNothing()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask scopedTask = ticket.Tasks.Single(task => task.Id == 20);
        ImplTaskCreationApiConnection apiConnection = new(ticket) { ReturnInvalidSnapshotProof = true };
        WfHandler handler = CreateHandler(ticket, apiConnection);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.CreateMissingImplTasksForTicket(ticket, scopedTask));

        Assert.Multiple(() =>
        {
            Assert.That(Element(ticket, 201).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(Element(ticket, 301).IpString, Is.EqualTo("10.0.0.10/32"));
            Assert.That(apiConnection.InsertCount, Is.Zero);
            Assert.That(ticket.Tasks.SelectMany(task => task.ImplementationTasks), Is.Empty);
        });
    }

    [Test]
    public async Task CreateMissingImplTasksForTicket_CurrentTicketSkipsRefreshMutation()
    {
        WfTicket ticket = CreateTicket();
        WfReqTask scopedTask = ticket.Tasks.Single(task => task.Id == 20);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(Element(ticket, 201), Element(ticket, 101));
        WfObjectTaskHelper.CopyRequestedStateSnapshot(Element(ticket, 301), Element(ticket, 101));
        ImplTaskCreationApiConnection apiConnection = new(ticket);
        WfHandler handler = CreateHandler(ticket, apiConnection);

        await handler.CreateMissingImplTasksForTicket(ticket, scopedTask);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.SnapshotMutationCount, Is.Zero);
            Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        });
    }

    private static WfHandler CreateHandler(WfTicket ticket, ImplTaskCreationApiConnection apiConnection,
        bool considerBundling = false, ReqDeviceInputOptions deviceInput = ReqDeviceInputOptions.oneTaskForAllDevices)
    {
        SimulatedUserConfig userConfig = new()
        {
            ReqConsiderBundling = considerBundling,
            ReqDeviceInput = deviceInput
        };
        WfHandler handler = new()
        {
            userConfig = userConfig,
            ActTicket = ticket,
            AutoPlanSettingsOverride = new GlobalDeviceSpecificSettings { AutoPlanImplementationForRules = true },
            MasterStateMatrix = new StateMatrix
            {
                LowestEndState = 3,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            }
        };
        SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
        {
            LowestInputState = 0,
            LowestStartedState = 2,
            LowestEndState = 10,
            MinImplTasksNeeded = 3,
            MinTicketCompleted = 99,
            PhaseActive = new() { { WorkflowPhases.planning, false } }
        });
        ActionHandler actionHandler = new(apiConnection, handler);
        WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConnection, actionHandler,
            false, WorkflowPhases.request, false);
        FieldInfo field = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(WfHandler), "dbAcc");
        field.SetValue(handler, dbAccess);
        return handler;
    }

    private static WfTicket CreateTicket()
    {
        WfReqTask objectTask = new()
        {
            Id = 10,
            TicketId = 100,
            TaskType = WfTaskType.object_create.ToString(),
            StateId = 4,
            Elements =
            [
                new WfReqElement
                {
                    Id = 101,
                    TaskId = 10,
                    Field = ElemFieldType.source.ToString(),
                    Name = "current",
                    IpString = "10.0.1.10/32"
                }
            ]
        };
        WfTicket ticket = new()
        {
            Id = 100,
            StateId = 4,
            Requester = new UiUser { DbId = 7 },
            Tasks = [objectTask, AccessTask(20, 201), AccessTask(30, 301)]
        };
        ticket.UpdateCidrsInTaskElements();
        return ticket;
    }

    private static WfReqTask AccessTask(long taskId, long elementId)
    {
        return new WfReqTask
        {
            Id = taskId,
            TicketId = 100,
            TaskType = WfTaskType.access.ToString(),
            StateId = 4,
            Title = $"Access {taskId}",
            Elements =
            [
                new WfReqElement
                {
                    Id = elementId,
                    TaskId = taskId,
                    ObjectTaskId = 10,
                    Field = ElemFieldType.source.ToString(),
                    Name = "stale",
                    IpString = "10.0.0.10/32"
                }
            ]
        };
    }

    private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
    {
        FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
        ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = matrix;
    }

    private static WfReqElement Element(WfTicket ticket, long elementId)
    {
        return ticket.Tasks.SelectMany(task => task.Elements).Single(element => element.Id == elementId);
    }

    private static long UpdateId(Dictionary<string, object?> update)
    {
        Dictionary<string, object?> where = (Dictionary<string, object?>)update["where"]!;
        Dictionary<string, object?> id = (Dictionary<string, object?>)where["id"]!;
        return (long)id["_eq"]!;
    }

    private static int BatchIndex(ImplTaskCreationApiConnection apiConnection)
    {
        return apiConnection.QueryLog.IndexOf(FWO.Api.Client.Queries.RequestQueries.updateRequestElementSnapshots);
    }

    private static int FirstInsertIndex(ImplTaskCreationApiConnection apiConnection)
    {
        return apiConnection.QueryLog.IndexOf(FWO.Api.Client.Queries.RequestQueries.newImplementationTask);
    }
}
