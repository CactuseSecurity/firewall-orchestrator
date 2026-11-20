using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Test.Mocks;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class WfHandlerImplCreationBoundaryTest
{
    private static readonly GlobalDeviceSpecificSettings kAllAutoPlanned = new()
    {
        AutoPlanImplementationForRules = true
    };

    [Test]
    public async Task TicketScopeLoadsOnceAndRefreshesOnlyOpenTasksBeforeInsertion()
    {
        WfReqTask firstProducer = CreateObjectTask(31, "10.0.1.10");
        WfReqTask secondProducer = CreateObjectTask(32, "10.0.1.11");
        WfReqTask firstOpen = CreateReferencedAccessTask(11, 31, "10.0.0.10");
        WfReqTask secondOpen = CreateReferencedAccessTask(12, 32, "10.0.0.11");
        WfReqTask closed = CreateReferencedAccessTask(13, 31, "10.0.0.12");
        closed.StateId = 99;
        WfHandler handler = CreateHandler(false,
            firstProducer, secondProducer, firstOpen, secondOpen, closed);
        handler.ActTicket.UpdateCidrsInTaskElements();
        ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
        SetDbAccess(handler, apiConnection);

        await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

        List<long> expectedMaterializedElementIds = [firstOpen.Elements[0].Id, secondOpen.Elements[0].Id];
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.TicketReadCount, Is.EqualTo(1));
            Assert.That(apiConnection.MaterializationBatchCount, Is.EqualTo(1));
            Assert.That(apiConnection.MaterializedElementIds,
                Is.EqualTo(expectedMaterializedElementIds));
            Assert.That(apiConnection.InsertCount, Is.EqualTo(2));
            Assert.That(closed.ImplementationTasks, Is.Empty);
            Assert.That(apiConnection.ReadsAfterMaterializationStarted, Is.Zero);
        });
    }

    [Test]
    public void BatchFailureStopsAllImplementationWork()
    {
        WfReqTask producer = CreateObjectTask(31, "10.0.1.10");
        WfReqTask access = CreateReferencedAccessTask(11, 31, "10.0.0.10");
        WfHandler handler = CreateHandler(false, producer, access);
        handler.ActTicket.UpdateCidrsInTaskElements();
        RecordingRulePositioningWorkflowCaller caller =
            RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
        ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket)
        {
            MismatchMaterializationUpdate = true
        };
        SetDbAccess(handler, apiConnection, caller);

        Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
            await handler.CreateMissingImplTasksForTicket(handler.ActTicket));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.TicketReadCount, Is.EqualTo(1));
            Assert.That(apiConnection.MaterializationBatchCount, Is.EqualTo(1));
            Assert.That(apiConnection.InsertCount, Is.Zero);
            Assert.That(apiConnection.ReadsAfterMaterializationStarted, Is.Zero);
            Assert.That(caller.CalculationCount, Is.Zero);
            Assert.That(access.ImplementationTasks, Is.Empty);
        });
    }

    [Test]
    public async Task RetryAfterCommittedFailureReplansAndDoesNotDuplicateWork()
    {
        WfReqTask producer = CreateObjectTask(31, "10.0.1.10");
        WfReqTask access = CreateReferencedAccessTask(11, 31, "10.0.0.10");
        WfHandler handler = CreateHandler(false, producer, access);
        handler.ActTicket.UpdateCidrsInTaskElements();
        ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket)
        {
            MismatchMaterializationUpdate = true
        };
        SetDbAccess(handler, apiConnection);
        Assert.ThrowsAsync<EffectiveRequestStateMaterializationException>(async () =>
            await handler.CreateMissingImplTasksForTicket(handler.ActTicket));

        apiConnection.MismatchMaterializationUpdate = false;
        await handler.CreateMissingImplTasksForTicket(handler.ActTicket);
        await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.TicketReadCount, Is.EqualTo(3));
            Assert.That(apiConnection.MaterializationBatchCount, Is.EqualTo(1));
            Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
            Assert.That(handler.ActTicket.Tasks.Single(task => task.Id == access.Id).ImplementationTasks,
                Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RuntimeOnlyInterfaceTasksKeepInstancesAndPerformNoPersistence()
    {
        WfReqTask first = CreateRuntimeInterfaceTask(0, "first");
        WfReqTask second = CreateRuntimeInterfaceTask(-1, "second");
        WfHandler handler = CreateHandler(false, first, second);
        ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
        SetDbAccess(handler, apiConnection);

        await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

        List<WfReqTask> expectedTasks = [first, second];
        Assert.Multiple(() =>
        {
            Assert.That(handler.ActTicket.Tasks, Is.EqualTo(expectedTasks));
            Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(first));
            Assert.That(handler.ActTicket.Tasks[1], Is.SameAs(second));
            Assert.That(apiConnection.TicketReadCount, Is.Zero);
            Assert.That(apiConnection.MaterializationBatchCount, Is.Zero);
            Assert.That(apiConnection.InsertCount, Is.Zero);
            Assert.That(first.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(second.ImplementationTasks, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RequestScopedBundleWaitsWithoutLoadingOrWideningScope()
    {
        WfReqTask scopeTask = CreateAccessTask(11, 4, "bundle");
        WfReqTask waitingMember = CreateAccessTask(12, 3, "bundle");
        WfHandler handler = CreateHandler(true, scopeTask, waitingMember);
        ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
        SetDbAccess(handler, apiConnection);

        await handler.CreateMissingImplTasksForTicket(handler.ActTicket, scopeTask);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.TicketReadCount, Is.Zero);
            Assert.That(apiConnection.MaterializationBatchCount, Is.Zero);
            Assert.That(apiConnection.InsertCount, Is.Zero);
            Assert.That(scopeTask.ImplementationTasks, Is.Empty);
            Assert.That(waitingMember.ImplementationTasks, Is.Empty);
        });
    }

    [Test]
    public async Task PreparationUsesPersistedContentAndOnlySixRuntimeFields()
    {
        UiUser currentHandler = new() { DbId = 71 };
        UiUser recentHandler = new() { DbId = 72 };
        DateTime runtimeStart = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
        DateTime runtimeStop = runtimeStart.AddHours(1);
        WfReqTask runtimeTask = CreateAccessTask(11, 4, "");
        runtimeTask.Title = "runtime title";
        runtimeTask.Start = runtimeStart;
        runtimeTask.Stop = runtimeStop;
        runtimeTask.CurrentHandler = currentHandler;
        runtimeTask.RecentHandler = recentHandler;
        runtimeTask.AssignedGroup = "runtime group";
        runtimeTask.Elements[0].Name = "runtime element";

        WfReqTask storedTask = CreateAccessTask(11, 1, "");
        storedTask.Title = "stored title";
        storedTask.Elements[0].Name = "stored element";
        WfImplTask storedImplTask = new() { Id = 91, ReqTaskId = storedTask.Id };
        storedTask.ImplementationTasks.Add(storedImplTask);
        WfTicket storedTicket = new() { Id = 7, StateId = 1, Tasks = { storedTask } };
        WfHandler handler = CreateHandler(false, runtimeTask);
        handler.ActTicket.StateId = 8;
        ImplTaskCreationApiConnection apiConnection = new(storedTicket);
        SetDbAccess(handler, apiConnection);

        await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

        WfReqTask preparedTask = handler.ActTicket.Tasks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(8));
            Assert.That(preparedTask, Is.SameAs(storedTask));
            Assert.That(preparedTask.StateId, Is.EqualTo(4));
            Assert.That(preparedTask.Start, Is.EqualTo(runtimeStart));
            Assert.That(preparedTask.Stop, Is.EqualTo(runtimeStop));
            Assert.That(preparedTask.CurrentHandler, Is.SameAs(currentHandler));
            Assert.That(preparedTask.RecentHandler, Is.SameAs(recentHandler));
            Assert.That(preparedTask.AssignedGroup, Is.EqualTo("runtime group"));
            Assert.That(preparedTask.Title, Is.EqualTo("stored title"));
            Assert.That(preparedTask.Elements[0].Name, Is.EqualTo("stored element"));
            Assert.That(preparedTask.ImplementationTasks.Single(), Is.SameAs(storedImplTask));
            Assert.That(apiConnection.TicketReadCount, Is.EqualTo(1));
            Assert.That(apiConnection.MaterializationBatchCount, Is.Zero);
            Assert.That(apiConnection.InsertCount, Is.Zero);
        });
    }

    private static WfHandler CreateHandler(bool considerBundling, params WfReqTask[] tasks)
    {
        WfHandler handler = new()
        {
            userConfig = new SimulatedUserConfig
            {
                ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices,
                ReqConsiderBundling = considerBundling
            },
            AutoPlanSettingsOverride = kAllAutoPlanned,
            MasterStateMatrix = new StateMatrix
            {
                LowestEndState = 3,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            },
            ActTicket = new WfTicket { Id = 7, StateId = 3, Tasks = [.. tasks] }
        };
        SetMatrix(handler, WfTaskType.access.ToString());
        SetMatrix(handler, WfTaskType.new_interface.ToString());
        return handler;
    }

    private static void SetMatrix(WfHandler handler, string taskType)
    {
        FieldInfo? field = typeof(WfHandler).GetField(
            "stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
        StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
        dict.Matrices[taskType] = new StateMatrix
        {
            LowestInputState = 0,
            LowestStartedState = 2,
            LowestEndState = 10,
            MinImplTasksNeeded = 3,
            MinTicketCompleted = 99,
            PhaseActive = new() { { WorkflowPhases.planning, false } }
        };
    }

    private static void SetDbAccess(WfHandler handler, SimulatedApiConnection apiConnection,
        RecordingRulePositioningWorkflowCaller? caller = null)
    {
        ActionHandler actionHandler = caller == null
            ? new ActionHandler(apiConnection, handler)
            : new ActionHandler(apiConnection, handler, null, true, null, null, caller);
        if (caller != null)
        {
            List<WfState> states = [new WfState { Id = 4 }];
            actionHandler.Init(states).GetAwaiter().GetResult();
        }
        WfDbAccess dbAccess = new(DefaultInit.DoNothing, handler.userConfig, apiConnection,
            actionHandler, false, WorkflowPhases.request, false);
        FieldInfo? field = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(handler, dbAccess);
    }

    private static WfReqTask CreateObjectTask(long id, string ip)
    {
        return new WfReqTask
        {
            Id = id,
            TicketId = 7,
            TaskNumber = 1,
            StateId = 4,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = id * 10,
                    TaskId = id,
                    Field = ElemFieldType.source.ToString(),
                    RequestAction = RequestAction.create.ToString(),
                    Name = "object",
                    IpString = ip
                }
            ]
        };
    }

    private static WfReqTask CreateReferencedAccessTask(long id, long objectTaskId, string ip)
    {
        WfReqTask task = CreateAccessTask(id, 4, "");
        task.TaskNumber = 2;
        task.Elements[0].ObjectTaskId = objectTaskId;
        task.Elements[0].IpString = ip;
        return task;
    }

    private static WfReqTask CreateAccessTask(long id, int stateId, string bundleId)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 7,
            StateId = stateId,
            Title = $"Access {id}",
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Id = id * 10,
                    TaskId = id,
                    Field = ElemFieldType.source.ToString(),
                    Name = $"source-{id}",
                    IpString = $"10.0.0.{id}"
                }
            ]
        };
        if (!string.IsNullOrWhiteSpace(bundleId))
        {
            task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, bundleId);
        }
        return task;
    }

    private static WfReqTask CreateRuntimeInterfaceTask(long id, string title)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 0,
            StateId = 4,
            Title = title,
            TaskType = WfTaskType.new_interface.ToString()
        };
        return task;
    }
}
