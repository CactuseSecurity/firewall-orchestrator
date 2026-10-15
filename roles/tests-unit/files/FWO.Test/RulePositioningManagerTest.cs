using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class RulePositioningManagerTest
{
    private const int kCheckPointDeviceTypeId = 9;
    private const int kFortigateDeviceTypeId = 10;
    private const int kManagementId = 21;
    private const int kGatewayId = 31;
    private const long kRequestTaskId = 41;
    private const long kImplementationTaskId = 51;
    private const long kTicketId = 61;
    private const string kSensitiveCredential = "manager-secret-4711";
    private const string kCompleteRuleContent = "complete-rule-content-0815";

    [Test]
    public async Task CalculateAsync_SelectsOnlyMatchingCheckPointLikeStrategy()
    {
        RulePositioningContext context = ValidContext();
        RecordingStrategy checkPointStrategy = CheckPointStrategy();
        RecordingStrategy fortigateStrategy = FortigateStrategy();
        RulePositioningManager manager = new([checkPointStrategy, fortigateStrategy]);

        RulePositioningResult result = await manager.CalculateAsync(context, CancellationToken.None);

        Assert.That(((RulePositioningSuccess)result).Position, Is.TypeOf<CheckPointTestPosition>());
        AssertStrategyCalls(checkPointStrategy, 1, 1);
        AssertStrategyCalls(fortigateStrategy, 1, 0);
    }

    [Test]
    public async Task CalculateAsync_SelectsOnlyMatchingFortigateLikeStrategy()
    {
        RulePositioningContext context = ValidContext(
            kFortigateDeviceTypeId,
            ProvisioningPositioningAlgorithm.FortinetEndOfZone);
        RecordingStrategy checkPointStrategy = CheckPointStrategy();
        RecordingStrategy fortigateStrategy = FortigateStrategy();
        RulePositioningManager manager = new([checkPointStrategy, fortigateStrategy]);

        RulePositioningResult result = await manager.CalculateAsync(context, CancellationToken.None);

        Assert.That(((RulePositioningSuccess)result).Position, Is.TypeOf<FortigateTestPosition>());
        AssertStrategyCalls(checkPointStrategy, 1, 0);
        AssertStrategyCalls(fortigateStrategy, 1, 1);
    }

    [Test]
    public async Task CalculateAsync_UsesImplementationTaskManagementOverride()
    {
        RulePositioningContext context = ValidContext();
        int requestManagementId = kManagementId + 1;
        context.ImplementationTask.ManagementId = kManagementId;
        context.ParentRequestTask.ManagementId = requestManagementId;
        context.ParentRequestTask.OnManagement = new Management { Id = requestManagementId };
        RecordingStrategy strategy = CheckPointStrategy();

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(context, CancellationToken.None);

        Assert.That(result, Is.TypeOf<RulePositioningSuccess>());
        AssertStrategyCalls(strategy, 1, 1);
    }

    [Test]
    public async Task CalculateAsync_RejectsNullContextBeforeStrategyWork()
    {
        RecordingStrategy strategy = CheckPointStrategy();
        RulePositioningManager manager = new([strategy]);

        RulePositioningResult result = await manager.CalculateAsync(null!, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.InvalidInput);
        AssertStrategyCalls(strategy, 0, 0);
    }

    [TestCase(InvalidContextCase.MissingImplementationTask)]
    [TestCase(InvalidContextCase.MissingRequestTask)]
    [TestCase(InvalidContextCase.MissingGateway)]
    [TestCase(InvalidContextCase.MissingManagement)]
    [TestCase(InvalidContextCase.MissingDeviceType)]
    [TestCase(InvalidContextCase.InvalidDeviceType)]
    [TestCase(InvalidContextCase.MissingRequestTaskId)]
    [TestCase(InvalidContextCase.RequestTaskMismatch)]
    [TestCase(InvalidContextCase.NoRequestGateway)]
    [TestCase(InvalidContextCase.InvalidRequestGateway)]
    [TestCase(InvalidContextCase.MultipleRequestGateways)]
    [TestCase(InvalidContextCase.MissingImplementationGateway)]
    [TestCase(InvalidContextCase.ImplementationGatewayMismatch)]
    [TestCase(InvalidContextCase.SuppliedGatewayMismatch)]
    [TestCase(InvalidContextCase.MissingRequestManagement)]
    [TestCase(InvalidContextCase.RequestManagementMismatch)]
    [TestCase(InvalidContextCase.InvalidImplementationManagement)]
    [TestCase(InvalidContextCase.ImplementationManagementMismatch)]
    [TestCase(InvalidContextCase.InvalidSuppliedManagement)]
    [TestCase(InvalidContextCase.MissingGatewayManagement)]
    [TestCase(InvalidContextCase.GatewayManagementMismatch)]
    [TestCase(InvalidContextCase.LoadedRequestManagementMismatch)]
    public async Task CalculateAsync_RejectsContextMismatchBeforeStrategyWork(
        InvalidContextCase invalidContextCase)
    {
        RulePositioningContext context = InvalidContext(invalidContextCase);
        RecordingStrategy strategy = CheckPointStrategy();
        RulePositioningManager manager = new([strategy]);

        RulePositioningResult result = await manager.CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.InvalidInput);
        AssertStrategyCalls(strategy, 0, 0);
    }

    [TestCase(true, "rule_modify", "modify")]
    [TestCase(true, "rule_delete", "delete")]
    [TestCase(true, "group_create", "create")]
    [TestCase(true, "future_task", "future_action")]
    [TestCase(false, "rule_modify", "modify")]
    [TestCase(false, "rule_delete", "delete")]
    [TestCase(false, "group_create", "create")]
    [TestCase(false, "future_task", "future_action")]
    public async Task CalculateAsync_RejectsUnsupportedTaskBeforeStrategyWork(
        bool changeImplementationTask,
        string taskType,
        string action)
    {
        RulePositioningContext context = ValidContext();
        if (changeImplementationTask)
        {
            context.ImplementationTask.TaskType = taskType;
            context.ImplementationTask.ImplAction = action;
        }
        else
        {
            context.ParentRequestTask.TaskType = taskType;
            context.ParentRequestTask.RequestAction = action;
        }
        RecordingStrategy strategy = CheckPointStrategy();

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.UnsupportedTask);
        AssertStrategyCalls(strategy, 0, 0);
    }

    [Test]
    public async Task CalculateAsync_RejectsMissingEffectiveSettingsBeforeStrategyWork()
    {
        RulePositioningContext context = ValidContext() with { EffectiveSettings = null! };
        RecordingStrategy strategy = CheckPointStrategy();

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.ConfigurationInvalid);
        AssertStrategyCalls(strategy, 0, 0);
    }

    [TestCase(ProvisioningPositioningAlgorithm.Undefined)]
    [TestCase((ProvisioningPositioningAlgorithm)999)]
    public async Task CalculateAsync_RejectsUnusableAlgorithmBeforeStrategyWork(
        ProvisioningPositioningAlgorithm algorithm)
    {
        RulePositioningContext context = ValidContext();
        context.EffectiveSettings.PositioningAlgorithm = algorithm;
        RecordingStrategy strategy = CheckPointStrategy();

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.ConfigurationInvalid);
        AssertStrategyCalls(strategy, 0, 0);
    }

    [Test]
    public async Task CalculateAsync_ReturnsNotImplementedForEmptyProductionRegistrationSet()
    {
        RulePositioningManager manager = new([]);

        RulePositioningResult result = await manager.CalculateAsync(
            ValidContext(),
            CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.PositioningMethodNotImplemented);
    }

    [Test]
    public async Task CalculateAsync_DoesNotFallBackAcrossVendorOrAlgorithm()
    {
        RecordingStrategy wrongVendor = FortigateStrategy();
        RecordingStrategy wrongAlgorithm = new(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.DefaultEndOfRulebase,
            SuccessfulResult(new CheckPointTestPosition()));
        RulePositioningManager manager = new([wrongVendor, wrongAlgorithm]);

        RulePositioningResult result = await manager.CalculateAsync(
            ValidContext(),
            CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.PositioningMethodNotImplemented);
        AssertStrategyCalls(wrongVendor, 1, 0);
        AssertStrategyCalls(wrongAlgorithm, 1, 0);
    }

    [Test]
    public async Task CalculateAsync_RejectsDuplicateMatchesWithoutCalculation()
    {
        RecordingStrategy first = CheckPointStrategy();
        RecordingStrategy second = CheckPointStrategy();
        RulePositioningManager manager = new([first, second]);

        RulePositioningResult result = await manager.CalculateAsync(
            ValidContext(),
            CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.StrategyRegistrationAmbiguous);
        AssertStrategyCalls(first, 1, 0);
        AssertStrategyCalls(second, 1, 0);
    }

    [Test]
    public async Task CalculateAsync_PreservesTypedStrategyFailureWithoutFallback()
    {
        RulePositioningFailure strategyFailure = new(RulePositioningFailureCategory.ConfigurationInvalid);
        RecordingStrategy matching = new(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            Task.FromResult<RulePositioningResult>(strategyFailure));
        RecordingStrategy nonmatching = FortigateStrategy();
        RulePositioningManager manager = new([matching, nonmatching]);

        RulePositioningResult result = await manager.CalculateAsync(
            ValidContext(),
            CancellationToken.None);

        Assert.That(result, Is.SameAs(strategyFailure));
        AssertStrategyCalls(matching, 1, 1);
        AssertStrategyCalls(nonmatching, 1, 0);
    }

    [Test]
    public async Task CalculateAsync_ConvertsUnexpectedCalculationExceptionToSafeFailure()
    {
        RecordingStrategy strategy = new(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            (_, _) => throw new InvalidOperationException(
                $"{kSensitiveCredential} {kCompleteRuleContent}"));

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(ValidContext(), CancellationToken.None);

        AssertSafeCalculationFailure(result);
        AssertStrategyCalls(strategy, 1, 1);
    }

    [Test]
    public async Task CalculateAsync_ConvertsUnassociatedCancellationToSafeFailure()
    {
        RecordingStrategy strategy = new(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            (_, _) => throw new OperationCanceledException());

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(ValidContext(), CancellationToken.None);

        AssertSafeCalculationFailure(result);
        AssertStrategyCalls(strategy, 1, 1);
    }

    [Test]
    public async Task CalculateAsync_ConvertsUnexpectedCapabilityExceptionToSafeFailure()
    {
        ThrowingCapabilityStrategy strategy = new();

        RulePositioningResult result = await new RulePositioningManager([strategy])
            .CalculateAsync(ValidContext(), CancellationToken.None);

        AssertSafeCalculationFailure(result);
        Assert.That(strategy.CalculationCallCount, Is.Zero);
    }

    [Test]
    public void CalculateAsync_PropagatesPreCancellationWithoutCalculation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        RecordingStrategy strategy = CheckPointStrategy();
        RulePositioningManager manager = new([strategy]);

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await manager.CalculateAsync(ValidContext(), cancellation.Token));

        AssertStrategyCalls(strategy, 1, 0);
    }

    [Test]
    public void CalculateAsync_PropagatesCancellationFromStrategy()
    {
        using CancellationTokenSource cancellation = new();
        RecordingStrategy strategy = new(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            (_, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<RulePositioningResult>(token);
            });
        RulePositioningManager manager = new([strategy]);

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await manager.CalculateAsync(ValidContext(), cancellation.Token));

        AssertStrategyCalls(strategy, 1, 1);
    }

    [Test]
    public async Task CalculateAsync_PassesSameContextAndTokenToStrategy()
    {
        using CancellationTokenSource cancellation = new();
        RulePositioningContext context = ValidContext();
        RecordingStrategy strategy = CheckPointStrategy();

        await new RulePositioningManager([strategy])
            .CalculateAsync(context, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(strategy.ReceivedContext, Is.SameAs(context));
            Assert.That(strategy.ReceivedToken, Is.EqualTo(cancellation.Token));
        });
    }

    [Test]
    public void Constructor_RejectsNullStrategyCollection()
    {
        Assert.Throws<ArgumentNullException>(() => new RulePositioningManager(null!));
    }

    /// <summary>
    /// Creates a valid caller-resolved context for one gateway-specific implementation task.
    /// </summary>
    private static RulePositioningContext ValidContext(
        int deviceTypeId = kCheckPointDeviceTypeId,
        ProvisioningPositioningAlgorithm algorithm =
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection)
    {
        Management management = new() { Id = kManagementId, Name = "Test management" };
        Device gateway = new()
        {
            Id = kGatewayId,
            Name = "Test gateway",
            DeviceType = new DeviceType { Id = deviceTypeId, Name = "Test device type" },
            Management = management
        };
        WfReqTask requestTask = new()
        {
            Id = kRequestTaskId,
            TicketId = kTicketId,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ManagementId = kManagementId,
            OnManagement = management
        };
        requestTask.SetDeviceList([kGatewayId]);
        WfImplTask implementationTask = new()
        {
            Id = kImplementationTaskId,
            ReqTaskId = kRequestTaskId,
            TicketId = kTicketId,
            TaskType = WfTaskType.access.ToString(),
            ImplAction = RequestAction.create.ToString(),
            DeviceId = kGatewayId
        };
        GatewayProvisioningSettings settings = new() { PositioningAlgorithm = algorithm };
        return new RulePositioningContext(
            implementationTask,
            requestTask,
            gateway,
            management,
            settings);
    }

    /// <summary>
    /// Creates one invalid variation while leaving all unrelated context valid.
    /// </summary>
    private static RulePositioningContext InvalidContext(InvalidContextCase invalidContextCase)
    {
        RulePositioningContext context = ValidContext();
        switch (invalidContextCase)
        {
            case InvalidContextCase.MissingImplementationTask:
                return context with { ImplementationTask = null! };
            case InvalidContextCase.MissingRequestTask:
                return context with { ParentRequestTask = null! };
            case InvalidContextCase.MissingGateway:
                return context with { TargetGateway = null! };
            case InvalidContextCase.MissingManagement:
                return context with { TargetManagement = null! };
            case InvalidContextCase.MissingDeviceType:
                context.TargetGateway.DeviceType = null!;
                break;
            case InvalidContextCase.InvalidDeviceType:
                context.TargetGateway.DeviceType.Id = 0;
                break;
            case InvalidContextCase.MissingRequestTaskId:
                context.ParentRequestTask.Id = 0;
                context.ImplementationTask.ReqTaskId = 0;
                break;
            case InvalidContextCase.RequestTaskMismatch:
                context.ImplementationTask.ReqTaskId++;
                break;
            case InvalidContextCase.NoRequestGateway:
                context.ParentRequestTask.SetDeviceList(new List<int>());
                break;
            case InvalidContextCase.InvalidRequestGateway:
                context.ParentRequestTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
                break;
            case InvalidContextCase.MultipleRequestGateways:
                context.ParentRequestTask.SetDeviceList([kGatewayId, kGatewayId + 1]);
                break;
            case InvalidContextCase.MissingImplementationGateway:
                context.ImplementationTask.DeviceId = null;
                break;
            case InvalidContextCase.ImplementationGatewayMismatch:
                context.ImplementationTask.DeviceId++;
                break;
            case InvalidContextCase.SuppliedGatewayMismatch:
                context.TargetGateway.Id++;
                break;
            case InvalidContextCase.MissingRequestManagement:
                context.ParentRequestTask.ManagementId = null;
                break;
            case InvalidContextCase.RequestManagementMismatch:
                context.ParentRequestTask.ManagementId++;
                break;
            case InvalidContextCase.InvalidImplementationManagement:
                context.ImplementationTask.ManagementId = 0;
                break;
            case InvalidContextCase.ImplementationManagementMismatch:
                context.ImplementationTask.ManagementId = kManagementId + 1;
                break;
            case InvalidContextCase.InvalidSuppliedManagement:
                context.TargetManagement.Id = 0;
                break;
            case InvalidContextCase.MissingGatewayManagement:
                context.TargetGateway.Management = null!;
                break;
            case InvalidContextCase.GatewayManagementMismatch:
                context.TargetGateway.Management = new Management { Id = kManagementId + 1 };
                break;
            case InvalidContextCase.LoadedRequestManagementMismatch:
                context.ParentRequestTask.OnManagement = new Management { Id = kManagementId + 1 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidContextCase));
        }
        return context;
    }

    /// <summary>
    /// Creates the test-only Check Point-like strategy.
    /// </summary>
    private static RecordingStrategy CheckPointStrategy()
    {
        CheckPointTestPosition position = new()
        {
            PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection
        };
        return new RecordingStrategy(
            kCheckPointDeviceTypeId,
            ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection,
            SuccessfulResult(position));
    }

    /// <summary>
    /// Creates the test-only Fortigate-like strategy.
    /// </summary>
    private static RecordingStrategy FortigateStrategy()
    {
        FortigateTestPosition position = new()
        {
            PositioningAlgorithm = ProvisioningPositioningAlgorithm.FortinetEndOfZone
        };
        return new RecordingStrategy(
            kFortigateDeviceTypeId,
            ProvisioningPositioningAlgorithm.FortinetEndOfZone,
            SuccessfulResult(position));
    }

    /// <summary>
    /// Wraps a test position in a completed successful calculation.
    /// </summary>
    private static Task<RulePositioningResult> SuccessfulResult(RulePosition position)
    {
        return Task.FromResult<RulePositioningResult>(new RulePositioningSuccess(position));
    }

    /// <summary>
    /// Verifies a typed failure category.
    /// </summary>
    private static void AssertFailure(
        RulePositioningResult result,
        RulePositioningFailureCategory expectedCategory)
    {
        Assert.That(result, Is.TypeOf<RulePositioningFailure>());
        Assert.That(
            ((RulePositioningFailure)result).Category,
            Is.EqualTo(expectedCategory));
    }

    /// <summary>
    /// Verifies that an unexpected exception produces no sensitive diagnostic content.
    /// </summary>
    private static void AssertSafeCalculationFailure(RulePositioningResult result)
    {
        AssertFailure(result, RulePositioningFailureCategory.CalculationFailed);
        string diagnostic = ((RulePositioningFailure)result).Diagnostic;
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic, Does.Not.Contain(kSensitiveCredential));
            Assert.That(diagnostic, Does.Not.Contain(kCompleteRuleContent));
        });
    }

    /// <summary>
    /// Verifies capability and calculation invocation counts.
    /// </summary>
    private static void AssertStrategyCalls(
        RecordingStrategy strategy,
        int expectedCapabilityCalls,
        int expectedCalculationCalls)
    {
        Assert.Multiple(() =>
        {
            Assert.That(strategy.CapabilityCallCount, Is.EqualTo(expectedCapabilityCalls));
            Assert.That(strategy.CalculationCallCount, Is.EqualTo(expectedCalculationCalls));
        });
    }

    public enum InvalidContextCase
    {
        MissingImplementationTask,
        MissingRequestTask,
        MissingGateway,
        MissingManagement,
        MissingDeviceType,
        InvalidDeviceType,
        MissingRequestTaskId,
        RequestTaskMismatch,
        NoRequestGateway,
        InvalidRequestGateway,
        MultipleRequestGateways,
        MissingImplementationGateway,
        ImplementationGatewayMismatch,
        SuppliedGatewayMismatch,
        MissingRequestManagement,
        RequestManagementMismatch,
        InvalidImplementationManagement,
        ImplementationManagementMismatch,
        InvalidSuppliedManagement,
        MissingGatewayManagement,
        GatewayManagementMismatch,
        LoadedRequestManagementMismatch
    }

    private sealed class RecordingStrategy : IRulePositioningStrategy
    {
        private readonly int deviceTypeId;
        private readonly ProvisioningPositioningAlgorithm algorithm;
        private readonly Func<RulePositioningContext, CancellationToken, Task<RulePositioningResult>> calculation;

        public int CapabilityCallCount { get; private set; }
        public int CalculationCallCount { get; private set; }
        public RulePositioningContext? ReceivedContext { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        /// <summary>
        /// Creates a recording strategy returning one fixed result.
        /// </summary>
        public RecordingStrategy(
            int deviceTypeId,
            ProvisioningPositioningAlgorithm algorithm,
            Task<RulePositioningResult> result)
            : this(deviceTypeId, algorithm, (_, _) => result)
        { }

        /// <summary>
        /// Creates a recording strategy using the supplied calculation callback.
        /// </summary>
        public RecordingStrategy(
            int deviceTypeId,
            ProvisioningPositioningAlgorithm algorithm,
            Func<RulePositioningContext, CancellationToken, Task<RulePositioningResult>> calculation)
        {
            this.deviceTypeId = deviceTypeId;
            this.algorithm = algorithm;
            this.calculation = calculation;
        }

        /// <summary>
        /// Records and evaluates an exact device-type and algorithm capability check.
        /// </summary>
        public bool CanHandle(
            DeviceType deviceType,
            ProvisioningPositioningAlgorithm positioningAlgorithm)
        {
            CapabilityCallCount++;
            return deviceType.Id == deviceTypeId && positioningAlgorithm == algorithm;
        }

        /// <summary>
        /// Records the exact calculation inputs and invokes the configured callback.
        /// </summary>
        public Task<RulePositioningResult> CalculateAsync(
            RulePositioningContext context,
            CancellationToken cancellationToken)
        {
            CalculationCallCount++;
            ReceivedContext = context;
            ReceivedToken = cancellationToken;
            return calculation(context, cancellationToken);
        }
    }

    private sealed class ThrowingCapabilityStrategy : IRulePositioningStrategy
    {
        public int CalculationCallCount { get; private set; }

        /// <summary>
        /// Simulates an unexpected registration failure containing sensitive content.
        /// </summary>
        public bool CanHandle(
            DeviceType deviceType,
            ProvisioningPositioningAlgorithm positioningAlgorithm)
        {
            throw new InvalidOperationException(
                $"{kSensitiveCredential} {kCompleteRuleContent}");
        }

        /// <summary>
        /// Records any erroneous attempt to calculate after capability selection fails.
        /// </summary>
        public Task<RulePositioningResult> CalculateAsync(
            RulePositioningContext context,
            CancellationToken cancellationToken)
        {
            CalculationCallCount++;
            return SuccessfulResult(new CheckPointTestPosition());
        }
    }

    [RulePositionType("test.check-point-position.v1")]
    private sealed class CheckPointTestPosition : RulePosition
    { }

    [RulePositionType("test.fortigate-position.v1")]
    private sealed class FortigateTestPosition : RulePosition
    { }
}
