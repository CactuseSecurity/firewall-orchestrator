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
    public void ProductionWorkflow_DoesNotInvokeCallerBeforeWp06()
    {
        string repository = LocateRepository();
        List<string> workflowFiles = Directory.GetFiles(
            Path.Combine(repository, "roles", "lib", "files", "FWO.Services", "Workflow"),
            "*.cs",
            SearchOption.AllDirectories).ToList();
        workflowFiles.AddRange(Directory.GetFiles(
            Path.Combine(repository, "roles", "middleware", "files", "FWO.Middleware.Server", "Controllers"),
            "*.cs",
            SearchOption.AllDirectories));

        Assert.That(workflowFiles.All(file =>
            !File.ReadAllText(file).Contains("IRulePositioningWorkflowCaller", StringComparison.Ordinal)), Is.True);
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

    private static string LocateRepository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "roles", "FWO.sln")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
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
