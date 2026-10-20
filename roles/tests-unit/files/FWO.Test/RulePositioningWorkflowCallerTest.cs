using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class RulePositioningWorkflowCallerTest
{
    private const int kManagementId = 11;
    private const int kGatewayId = 21;
    private const long kRequestTaskId = 31;
    private const long kImplementationTaskId = 41;

    [Test]
    public async Task CalculateAndPersistAsync_LoadsContextCalculatesOnceAndStoresPosition()
    {
        RecordingApiConnection api = new();
        RecordingStrategy strategy = new(Success());
        RulePositioningWorkflowCaller caller = Caller(api, strategy);
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();

        RulePositioningWorkflowOutcome outcome = await caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.Stored));
            Assert.That(outcome.FailureCategory, Is.Null);
            Assert.That(strategy.CalculationCount, Is.EqualTo(1));
            Assert.That(strategy.Context!.TargetGateway.Id, Is.EqualTo(kGatewayId));
            Assert.That(strategy.Context.TargetManagement.Id, Is.EqualTo(kManagementId));
            Assert.That(strategy.Context.EffectiveSettings.PositioningAlgorithm,
                Is.EqualTo(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
            Assert.That(api.TargetReads, Is.EqualTo(1));
            Assert.That(api.SettingsReads, Is.EqualTo(1));
            Assert.That(api.PositionWrites, Is.EqualTo(1));
            Assert.That(api.LastPosition, Is.TypeOf<CheckPointRulePosition>());
        });
    }

    [TestCase(RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulePositioningFailureCategory.UnsupportedTask)]
    [TestCase(RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(RulePositioningFailureCategory.PositioningMethodNotImplemented)]
    [TestCase(RulePositioningFailureCategory.StrategyRegistrationAmbiguous)]
    [TestCase(RulePositioningFailureCategory.CalculationFailed)]
    public async Task CalculateAndPersistAsync_ClearsEveryTypedCalculationFailure(
        RulePositioningFailureCategory category)
    {
        RecordingApiConnection api = new();
        RecordingStrategy strategy = new(new RulePositioningFailure(category));
        RulePositioningWorkflowCaller caller = Caller(api, strategy);
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();

        RulePositioningWorkflowOutcome outcome = await caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.ClearedAfterFailure));
            Assert.That(outcome.FailureCategory, Is.EqualTo(category));
            Assert.That(api.PositionWrites, Is.EqualTo(1));
            Assert.That(api.LastPosition, Is.Null);
        });
    }

    [Test]
    public async Task CalculateAndPersistAsync_ClearsAfterUnexpectedContextLoadFailure()
    {
        RecordingApiConnection api = new() { ThrowOnTargetRead = true };
        RecordingStrategy strategy = new(Success());
        RulePositioningWorkflowCaller caller = Caller(api, strategy);
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();

        RulePositioningWorkflowOutcome outcome = await caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.ClearedAfterFailure));
            Assert.That(outcome.FailureCategory, Is.EqualTo(RulePositioningFailureCategory.CalculationFailed));
            Assert.That(strategy.CalculationCount, Is.Zero);
            Assert.That(api.PositionWrites, Is.EqualTo(1));
            Assert.That(api.LastPosition, Is.Null);
        });
    }

    [Test]
    public async Task CalculateAndPersistAsync_ReturnsPersistenceFailedWithoutRetry()
    {
        RecordingApiConnection api = new() { ThrowOnPositionWrite = true };
        RulePositioningWorkflowCaller caller = Caller(api, new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();

        RulePositioningWorkflowOutcome outcome = await caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.PersistenceFailed));
            Assert.That(api.PositionWrites, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CalculateAndPersistAsync_SkipsInapplicableTaskWithoutApiWork()
    {
        RecordingApiConnection api = new();
        RecordingStrategy strategy = new(Success());
        RulePositioningWorkflowCaller caller = Caller(api, strategy);
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        implementationTask.DeviceId = null;

        RulePositioningWorkflowOutcome outcome = await caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.NotApplicable));
            Assert.That(api.AllCalls, Is.Zero);
            Assert.That(strategy.CalculationCount, Is.Zero);
        });
    }

    [Test]
    public void CalculateAndPersistAsync_CancellationBeforeWritePreservesExistingPosition()
    {
        RecordingApiConnection api = new();
        RecordingStrategy strategy = new(Success());
        RulePositioningWorkflowCaller caller = Caller(api, strategy);
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            cancellation.Token));
        Assert.That(api.PositionWrites, Is.Zero);
    }

    [Test]
    public async Task CalculateAndPersistAsync_AwaitsWriteAlreadyInProgressAfterCancellation()
    {
        RecordingApiConnection api = new() { DelayPositionWrite = true };
        RulePositioningWorkflowCaller caller = Caller(api, new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        using CancellationTokenSource cancellation = new();

        Task<RulePositioningWorkflowOutcome> call = caller.CalculateAndPersistAsync(
            requestTask,
            implementationTask,
            cancellation.Token);
        await api.WriteStarted.Task;
        cancellation.Cancel();
        Assert.That(call.IsCompleted, Is.False);
        api.CompletePositionWrite();

        RulePositioningWorkflowOutcome outcome = await call;
        Assert.That(outcome.Status, Is.EqualTo(RulePositioningWorkflowStatus.Stored));
    }

    [Test]
    public void IsApplicable_AcceptsValidPersistedAccessRuleCreation()
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.True);
    }

    [TestCase(0, kImplementationTaskId, kRequestTaskId)]
    [TestCase(kRequestTaskId, 0, kRequestTaskId)]
    [TestCase(kRequestTaskId, kImplementationTaskId, 999)]
    public void IsApplicable_RejectsZeroOrMismatchedIds(long requestTaskId, long implementationTaskId, long parentId)
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.Id = requestTaskId;
        implementationTask.Id = implementationTaskId;
        implementationTask.ReqTaskId = parentId;

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [TestCase(null)]
    [TestCase(0)]
    public void IsApplicable_RejectsMissingConcreteGateway(int? gatewayId)
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        implementationTask.DeviceId = gatewayId;

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [Test]
    public void IsApplicable_RejectsMissingEffectiveManagement()
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.ManagementId = null;
        implementationTask.ManagementId = null;

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [TestCase(WfTaskType.generic, WfTaskType.access)]
    [TestCase(WfTaskType.access, WfTaskType.generic)]
    [TestCase(WfTaskType.object_create, WfTaskType.object_create)]
    public void IsApplicable_RejectsUnsupportedTaskTypes(WfTaskType requestType, WfTaskType implementationType)
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.TaskType = requestType.ToString();
        implementationTask.TaskType = implementationType.ToString();

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [TestCase(RequestAction.modify, RequestAction.create)]
    [TestCase(RequestAction.delete, RequestAction.create)]
    [TestCase(RequestAction.create, RequestAction.modify)]
    [TestCase(RequestAction.create, RequestAction.delete)]
    public void IsApplicable_RejectsNonCreationActions(RequestAction requestAction, RequestAction implementationAction)
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.RequestAction = requestAction.ToString();
        implementationTask.ImplAction = implementationAction.ToString();

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [Test]
    public void IsApplicable_RejectsGatewayOutsideFiniteParentSelection()
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.SetDeviceList([999]);

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IsApplicable_AcceptsEmptyOrAllDeviceParentSelection(bool allDevices)
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.SetDeviceList(allDevices ? [WfReqTaskBase.kAllDevicesId] : []);

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.True);
    }

    [Test]
    public void IsApplicable_RejectsInconsistentRequestManagement()
    {
        RulePositioningWorkflowCaller caller = Caller(new(), new RecordingStrategy(Success()));
        (WfReqTask requestTask, WfImplTask implementationTask) = Tasks();
        requestTask.OnManagement = new Management { Id = 999 };

        Assert.That(caller.IsApplicable(requestTask, implementationTask), Is.False);
    }

    private static RulePositioningWorkflowCaller Caller(
        RecordingApiConnection api,
        RecordingStrategy strategy)
    {
        return new RulePositioningWorkflowCaller(
            api,
            new RulePositioningManager([strategy]),
            new ProvisioningSettingsManager(api));
    }

    private static RulePositioningResult Success()
    {
        return new RulePositioningSuccess(new CheckPointRulePosition(
            "layer",
            CheckPointPositionAnchorKind.Bottom,
            null,
            ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
    }

    private static (WfReqTask RequestTask, WfImplTask ImplementationTask) Tasks()
    {
        WfReqTask requestTask = new()
        {
            Id = kRequestTaskId,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ManagementId = kManagementId
        };
        requestTask.SetDeviceList([kGatewayId]);
        return (requestTask, new WfImplTask
        {
            Id = kImplementationTaskId,
            ReqTaskId = kRequestTaskId,
            DeviceId = kGatewayId,
            TaskType = WfTaskType.access.ToString(),
            ImplAction = RequestAction.create.ToString()
        });
    }

    private sealed class RecordingStrategy(RulePositioningResult result) : IRulePositioningStrategy
    {
        public int CalculationCount { get; private set; }
        public RulePositioningContext? Context { get; private set; }

        public bool CanHandle(DeviceType deviceType, ProvisioningPositioningAlgorithm positioningAlgorithm)
        {
            return deviceType.Id == 9
                && positioningAlgorithm == ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;
        }

        public Task<RulePositioningResult> CalculateAsync(
            RulePositioningContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CalculationCount++;
            Context = context;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingApiConnection : SimulatedApiConnection
    {
        private readonly TaskCompletionSource<ReturnId> writeCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnTargetRead { get; set; }
        public bool ThrowOnPositionWrite { get; set; }
        public bool DelayPositionWrite { get; set; }
        public int TargetReads { get; private set; }
        public int SettingsReads { get; private set; }
        public int PositionWrites { get; private set; }
        public int AllCalls => TargetReads + SettingsReads + PositionWrites;
        public RulePosition? LastPosition { get; private set; }
        public TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void CompletePositionWrite()
        {
            writeCompletion.TrySetResult(new ReturnId { UpdatedIdLong = kImplementationTaskId });
        }

        public override async Task<T> SendQueryAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == DeviceQueries.getRulePositioningTarget)
            {
                TargetReads++;
                if (ThrowOnTargetRead)
                {
                    throw new InvalidOperationException("sensitive target load failure");
                }
                return (T)(object)new List<Device> { Target() };
            }
            if (query == ProvisioningQueries.getNodesByNaturalKeys)
            {
                SettingsReads++;
                return (T)(object)new List<ProvisioningConfigNodeData>();
            }
            if (query == RequestQueries.updateImplementationTaskRulePosition)
            {
                PositionWrites++;
                LastPosition = ReadVariable<RulePosition?>(variables, "rulePosition");
                if (ThrowOnPositionWrite)
                {
                    throw new InvalidOperationException("sensitive persistence failure");
                }
                if (DelayPositionWrite)
                {
                    WriteStarted.TrySetResult();
                    return (T)(object)await writeCompletion.Task;
                }
                return (T)(object)new ReturnId { UpdatedIdLong = kImplementationTaskId };
            }
            throw new InvalidOperationException("Unexpected query in rule-positioning caller test.");
        }

        private static Device Target()
        {
            Management management = new()
            {
                Id = kManagementId,
                Name = "Management",
                DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" }
            };
            return new Device
            {
                Id = kGatewayId,
                Name = "Gateway",
                DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" },
                Management = management
            };
        }

        private static TValue ReadVariable<TValue>(object? variables, string name)
        {
            PropertyInfo property = variables!.GetType().GetProperty(name)
                ?? throw new InvalidOperationException($"Missing variable {name}.");
            return (TValue)property.GetValue(variables)!;
        }
    }
}
