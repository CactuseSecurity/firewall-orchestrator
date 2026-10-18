using FWO.Compliance.RulePositioning;
using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace FWO.Test;

[TestFixture]
internal class CheckPointRulePositioningStrategyTest
{
    private const int kManagementId = 21;
    private const int kGatewayId = 31;
    private const int kCriterionId = 41;
    private const int kCheckPointDeviceTypeId = 9;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection;
    private const string kSentinel = "strategy-secret-sentinel";

    [Test]
    public void CanHandle_UsesExactDeviceTypeAndCatalogCapability()
    {
        StrategyFixture fixture = new();

        bool result = fixture.Strategy.CanHandle(new DeviceType { Id = kCheckPointDeviceTypeId }, kAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(fixture.Catalog.SupportCallCount, Is.EqualTo(1));
            Assert.That(fixture.Catalog.SupportedAlgorithms, Is.EqualTo([kAlgorithm]));
        });
    }

    [TestCase(null, ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection)]
    [TestCase(13, ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection)]
    [TestCase(10, ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection)]
    [TestCase(9, ProvisioningPositioningAlgorithm.Undefined)]
    [TestCase(9, (ProvisioningPositioningAlgorithm)999)]
    public void CanHandle_RejectsNonCapabilityWithoutConsultingCatalog(
        int? deviceTypeId,
        ProvisioningPositioningAlgorithm algorithm)
    {
        StrategyFixture fixture = new();
        DeviceType? deviceType = deviceTypeId.HasValue ? new DeviceType { Id = deviceTypeId.Value } : null;

        bool result = fixture.Strategy.CanHandle(deviceType!, algorithm);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(fixture.Catalog.SupportCallCount, Is.Zero);
        });
    }

    [Test]
    public void CanHandle_ReturnsFalseForCatalogUnsupportedAlgorithm()
    {
        StrategyFixture fixture = new();
        fixture.Catalog.Supports = false;

        Assert.That(fixture.Strategy.CanHandle(new DeviceType { Id = 9 }, kAlgorithm), Is.False);
        Assert.That(fixture.Catalog.SupportCallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CapabilityException_EscapesDirectCallAndManagerMapsItSafely()
    {
        StrategyFixture fixture = new();
        fixture.Catalog.SupportException = new InvalidOperationException(kSentinel);

        Assert.Throws<InvalidOperationException>(() => fixture.Strategy.CanHandle(
            new DeviceType { Id = kCheckPointDeviceTypeId }, kAlgorithm));

        RulePositioningResult result = await new RulePositioningManager([fixture.Strategy])
            .CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.CalculationFailed);
        Assert.Multiple(() =>
        {
            Assert.That(((RulePositioningFailure)result).Diagnostic, Does.Not.Contain(kSentinel));
            Assert.That(fixture.Graph.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task CalculateAsync_ExecutesCompleteRecipeOnceInExactOrderWithOriginalInputs()
    {
        List<string> order = [];
        RulePositioningContext context = ValidContext();
        RulebaseGraphSnapshot snapshot = Snapshot(kManagementId, kGatewayId, "layer-uid");
        CreationRuleZoneInput zoneInput = ZoneInput();
        List<ComplianceNetworkZone> zones = [new() { Id = 1, CriterionId = kCriterionId, Name = "Zone" }];
        using CancellationTokenSource cancellation = new();
        StrategyFixture fixture = new(order)
        {
            Snapshot = snapshot,
            Zones = zones,
            ZoneInput = zoneInput
        };
        fixture.Catalog.Recipe = SuccessfulRecipe(order);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(context, cancellation.Token);

        RulePositioningSuccess success = (RulePositioningSuccess)result;
        CheckPointRulePosition position = (CheckPointRulePosition)success.Position;
        Assert.Multiple(() =>
        {
            Assert.That(order, Is.EqualTo(["graph", "matrix", "resolve", "select", "execute"]));
            Assert.That(fixture.Graph.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Matrix.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Resolver.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Catalog.SelectCallCount, Is.EqualTo(1));
            Assert.That(fixture.RootExecutionCount, Is.EqualTo(1));
            Assert.That(fixture.Graph.ManagementId, Is.EqualTo(kManagementId));
            Assert.That(fixture.Graph.GatewayId, Is.EqualTo(kGatewayId));
            Assert.That(fixture.Resolver.RequestTask, Is.SameAs(context.ParentRequestTask));
            Assert.That(fixture.Resolver.Zones, Is.SameAs(((DesignatedZoneMatrixReadSuccess)fixture.Matrix.Result).NetworkZones));
            Assert.That(fixture.Catalog.ZoneInput, Is.SameAs(zoneInput));
            Assert.That(fixture.Catalog.SelectedAlgorithm, Is.EqualTo(kAlgorithm));
            Assert.That(fixture.ExecutionContext!.CalculationContext, Is.SameAs(context));
            Assert.That(fixture.ExecutionContext.Snapshot, Is.SameAs(snapshot));
            Assert.That(fixture.Graph.Token, Is.EqualTo(cancellation.Token));
            Assert.That(fixture.Matrix.Token, Is.EqualTo(cancellation.Token));
            Assert.That(fixture.Resolver.Token, Is.EqualTo(cancellation.Token));
            Assert.That(fixture.ExecutionToken, Is.EqualTo(cancellation.Token));
            Assert.That(position, Is.SameAs(fixture.ReturnedPosition));
            Assert.That(position.PositioningAlgorithm, Is.EqualTo(kAlgorithm));
        });

        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> SuccessfulRecipe(List<string> events)
        {
            IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> structural =
                new EndOfBlock(new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()));
            StrategyFixture thisFixture = fixture;
            RecordingPassThroughBlock root = new(structural, events, invocation =>
            {
                thisFixture.ExecutionContext = invocation.Context;
                thisFixture.ExecutionToken = invocation.Token;
                thisFixture.RootExecutionCount++;
                thisFixture.ReturnedPosition = ((CheckPointPositionValue)invocation.ChildValues[0]).Position;
            });
            return new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(root);
        }
    }

    [Test]
    public async Task CalculateAsync_RejectsWrongSuccessfulValueAndAlgorithmMismatch()
    {
        StrategyFixture wrongValue = new();
        wrongValue.Catalog.Recipe = new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(
            new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()));

        RulePositioningResult wrongValueResult = await wrongValue.Strategy.CalculateAsync(
            ValidContext(), CancellationToken.None);

        RulePositioningContext changedContext = ValidContext();
        StrategyFixture mismatch = new();
        mismatch.Catalog.SelectionAction = () => changedContext.EffectiveSettings.PositioningAlgorithm =
            ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;
        mismatch.Catalog.Recipe = StructuralRecipe();
        RulePositioningResult mismatchResult = await mismatch.Strategy.CalculateAsync(
            changedContext, CancellationToken.None);

        AssertFailure(wrongValueResult, RulePositioningFailureCategory.CalculationFailed);
        AssertFailure(mismatchResult, RulePositioningFailureCategory.CalculationFailed);
    }

    [TestCase(DirectInvalidContext.NullContext)]
    [TestCase(DirectInvalidContext.MissingGateway)]
    [TestCase(DirectInvalidContext.MissingDeviceType)]
    [TestCase(DirectInvalidContext.MissingManagement)]
    [TestCase(DirectInvalidContext.MissingRequestTask)]
    [TestCase(DirectInvalidContext.MissingSettings)]
    [TestCase(DirectInvalidContext.InvalidGatewayId)]
    [TestCase(DirectInvalidContext.InvalidManagementId)]
    public async Task CalculateAsync_RejectsInvalidDirectContextBeforeDependencies(DirectInvalidContext invalid)
    {
        StrategyFixture fixture = new();
        RulePositioningContext? context = InvalidContext(invalid);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(context!, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.InvalidInput);
        Assert.That(fixture.Graph.CallCount, Is.Zero);
    }

    [TestCase(ProvisioningPositioningAlgorithm.Undefined)]
    [TestCase((ProvisioningPositioningAlgorithm)999)]
    public async Task CalculateAsync_RejectsInvalidAlgorithmAsConfiguration(
        ProvisioningPositioningAlgorithm algorithm)
    {
        StrategyFixture fixture = new();
        RulePositioningContext context = ValidContext();
        context.EffectiveSettings.PositioningAlgorithm = algorithm;

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.ConfigurationInvalid);
        Assert.That(fixture.Catalog.SupportCallCount, Is.Zero);
        Assert.That(fixture.Graph.CallCount, Is.Zero);
    }

    [TestCase(13)]
    [TestCase(10)]
    public async Task CalculateAsync_RejectsWrongDeviceBeforeLoadingData(int deviceTypeId)
    {
        StrategyFixture fixture = new();
        RulePositioningContext context = ValidContext();
        context.TargetGateway.DeviceType.Id = deviceTypeId;

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(context, CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.PositioningMethodNotImplemented);
        Assert.That(fixture.Graph.CallCount, Is.Zero);
    }

    [Test]
    public async Task CalculateAsync_RejectsCatalogUnsupportedAlgorithmBeforeLoadingData()
    {
        StrategyFixture fixture = new();
        fixture.Catalog.Supports = false;

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.PositioningMethodNotImplemented);
        Assert.That(fixture.Graph.CallCount, Is.Zero);
    }

    [TestCase(RulebaseGraphReadFailureCategory.InvalidInput, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulebaseGraphReadFailureCategory.ScopeMismatch, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulebaseGraphReadFailureCategory.NoSuccessfulImport, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(RulebaseGraphReadFailureCategory.InvalidGraphData, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(RulebaseGraphReadFailureCategory.QueryFailed, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase((RulebaseGraphReadFailureCategory)999, RulePositioningFailureCategory.CalculationFailed)]
    public async Task CalculateAsync_MapsGraphFailuresAndStops(
        RulebaseGraphReadFailureCategory childCategory,
        RulePositioningFailureCategory expected)
    {
        StrategyFixture fixture = new();
        fixture.Graph.Result = new RulebaseGraphReadFailure(childCategory);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, expected);
        AssertDownstream(fixture, graph: 1, matrix: 0, resolver: 0, selection: 0, execution: 0);
    }

    [TestCase(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid, RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable, RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(DesignatedZoneMatrixReadFailureCategory.QueryFailed, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase((DesignatedZoneMatrixReadFailureCategory)999, RulePositioningFailureCategory.CalculationFailed)]
    public async Task CalculateAsync_MapsMatrixFailuresAndStops(
        DesignatedZoneMatrixReadFailureCategory childCategory,
        RulePositioningFailureCategory expected)
    {
        StrategyFixture fixture = new();
        fixture.Matrix.Result = new DesignatedZoneMatrixReadFailure(childCategory);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, expected);
        AssertDownstream(fixture, graph: 1, matrix: 1, resolver: 0, selection: 0, execution: 0);
    }

    [TestCase(CreationRuleZoneResolutionFailureCategory.InvalidInput, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.UnsupportedObject, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.InvalidAddress, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.UnsupportedTask, RulePositioningFailureCategory.UnsupportedTask)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.InvalidZoneData, RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.GroupUnresolved, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.GroupQueryFailed, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(CreationRuleZoneResolutionFailureCategory.UnexpectedFailure, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase((CreationRuleZoneResolutionFailureCategory)999, RulePositioningFailureCategory.CalculationFailed)]
    public async Task CalculateAsync_MapsZoneFailuresAndStops(
        CreationRuleZoneResolutionFailureCategory childCategory,
        RulePositioningFailureCategory expected)
    {
        StrategyFixture fixture = new();
        fixture.Resolver.Result = new CreationRuleZoneResolutionFailure(childCategory);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, expected);
        AssertDownstream(fixture, graph: 1, matrix: 1, resolver: 1, selection: 0, execution: 0);
    }

    [TestCase(CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe, RulePositioningFailureCategory.PositioningMethodNotImplemented)]
    [TestCase(CheckPointRecipeSelectionFailureCategory.CatalogInvalid, RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase((CheckPointRecipeSelectionFailureCategory)999, RulePositioningFailureCategory.ConfigurationInvalid)]
    public async Task CalculateAsync_MapsSelectionFailuresAndStops(
        CheckPointRecipeSelectionFailureCategory childCategory,
        RulePositioningFailureCategory expected)
    {
        StrategyFixture fixture = new();
        fixture.Catalog.SelectionResult = new CheckPointRecipeSelectionFailure(childCategory);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, expected);
        AssertDownstream(fixture, graph: 1, matrix: 1, resolver: 1, selection: 1, execution: 0);
    }

    [TestCase(PositioningBlockFailureCategory.InvalidRecipe, RulePositioningFailureCategory.ConfigurationInvalid)]
    [TestCase(PositioningBlockFailureCategory.InvalidSnapshot, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(PositioningBlockFailureCategory.ScopeNotFound, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(PositioningBlockFailureCategory.ScopeAmbiguous, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(PositioningBlockFailureCategory.NoInsertionPoint, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase(PositioningBlockFailureCategory.UnexpectedExecutionFailure, RulePositioningFailureCategory.CalculationFailed)]
    [TestCase((PositioningBlockFailureCategory)999, RulePositioningFailureCategory.CalculationFailed)]
    public async Task CalculateAsync_MapsBlockFailuresAfterOneExecution(
        PositioningBlockFailureCategory childCategory,
        RulePositioningFailureCategory expected)
    {
        StrategyFixture fixture = new();
        RecordingResultBlock block = new((_, _, _) => Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
            new PositioningBlockFailure<CheckPointBlockValue>(childCategory, "test-only")));
        fixture.Catalog.Recipe = new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(block);

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, expected);
        Assert.Multiple(() =>
        {
            Assert.That(block.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Catalog.SelectCallCount, Is.EqualTo(1));
        });
    }

    [TestCase("graph")]
    [TestCase("matrix")]
    [TestCase("resolver")]
    [TestCase("selection")]
    [TestCase("execution")]
    public async Task CalculateAsync_MapsUnexpectedExceptionsWithoutSensitiveContent(string stage)
    {
        StrategyFixture fixture = new();
        if (stage == "graph") fixture.Graph.Exception = new InvalidOperationException(kSentinel);
        if (stage == "matrix") fixture.Matrix.Exception = new InvalidOperationException(kSentinel);
        if (stage == "resolver") fixture.Resolver.Exception = new InvalidOperationException(kSentinel);
        if (stage == "selection") fixture.Catalog.SelectionException = new InvalidOperationException(kSentinel);
        if (stage == "execution")
        {
            fixture.Catalog.Recipe = new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(
                new RecordingResultBlock((_, _, _) => throw new InvalidOperationException(kSentinel)));
        }

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.CalculationFailed);
        Assert.That(((RulePositioningFailure)result).Diagnostic, Does.Not.Contain(kSentinel));
    }

    [Test]
    public void CalculateAsync_PropagatesPreCancellationBeforeDependencyWork()
    {
        StrategyFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await fixture.Strategy.CalculateAsync(ValidContext(), cancellation.Token));
        Assert.That(fixture.Catalog.SupportCallCount, Is.Zero);
        Assert.That(fixture.Graph.CallCount, Is.Zero);
    }

    [TestCase("graph")]
    [TestCase("matrix")]
    [TestCase("resolver")]
    [TestCase("execution")]
    public async Task CalculateAsync_PropagatesInFlightCallerCancellation(string stage)
    {
        StrategyFixture fixture = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (stage == "graph") fixture.Graph.Pending = started;
        if (stage == "matrix") fixture.Matrix.Pending = started;
        if (stage == "resolver") fixture.Resolver.Pending = started;
        if (stage == "execution")
        {
            RecordingResultBlock block = new(async (_, _, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            });
            fixture.Catalog.Recipe = new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(block);
        }
        using CancellationTokenSource cancellation = new();
        Task<RulePositioningResult> calculation = fixture.Strategy.CalculateAsync(ValidContext(), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await calculation);
    }

    [Test]
    public void CalculateAsync_ObservesCancellationTriggeredDuringSynchronousSelection()
    {
        StrategyFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        fixture.Catalog.SelectionAction = cancellation.Cancel;

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await fixture.Strategy.CalculateAsync(ValidContext(), cancellation.Token));
        Assert.That(fixture.RootExecutionCount, Is.Zero);
    }

    [Test]
    public async Task CalculateAsync_MapsUnrelatedCancellationToCalculationFailure()
    {
        StrategyFixture fixture = new();
        fixture.Graph.Exception = new OperationCanceledException(new CancellationToken(true));

        RulePositioningResult result = await fixture.Strategy.CalculateAsync(ValidContext(), CancellationToken.None);

        AssertFailure(result, RulePositioningFailureCategory.CalculationFailed);
    }

    [Test]
    public void CalculateAsync_PropagatesExceptionCarryingSameCancellableToken()
    {
        StrategyFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        fixture.Graph.Exception = new OperationCanceledException(cancellation.Token);

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await fixture.Strategy.CalculateAsync(ValidContext(), cancellation.Token));
        Assert.That(cancellation.IsCancellationRequested, Is.False);
    }

    [Test]
    public async Task CalculateAsync_IsSafeForConcurrentCalculationsOnOneInstance()
    {
        ConcurrentBag<(int ManagementId, int GatewayId)> reads = [];
        DynamicGraphReader graph = new(reads);
        RecordingMatrixReader matrix = new(new DesignatedZoneMatrixReadSuccess([]));
        DynamicZoneResolver resolver = new();
        RecordingCatalog catalog = new() { Recipe = StructuralRecipe() };
        CheckPointRulePositioningStrategy strategy = new(graph, matrix, resolver, catalog);
        RulePositioningContext first = ValidContext(101, 201);
        RulePositioningContext second = ValidContext(102, 202);

        RulePositioningResult[] results = await Task.WhenAll(
            strategy.CalculateAsync(first, CancellationToken.None),
            strategy.CalculateAsync(second, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(results.Cast<RulePositioningSuccess>().Select(result =>
                ((CheckPointRulePosition)result.Position).LayerUid),
                Is.EquivalentTo(["layer-201", "layer-202"]));
            Assert.That(reads, Is.EquivalentTo(new[] { (101, 201), (102, 202) }));
            Assert.That(matrix.CallCount, Is.EqualTo(2));
            Assert.That(resolver.ReceivedTaskIds, Is.EquivalentTo(new long[] { 301, 302 }));
        });
    }

    [Test]
    public void Program_RegistersOnlyReusablePositioningReaders()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "roles", "middleware", "files", "FWO.Middleware.Server", "Program.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("AddSingleton<IDesignatedZoneMatrixReader, DesignatedZoneMatrixReader>()"));
            Assert.That(source, Does.Contain("AddSingleton<IRulebaseGraphSnapshotReader, RulebaseGraphSnapshotReader>()"));
            Assert.That(source, Does.Contain("AddSingleton<ICreationRuleZoneResolver, CreationRuleZoneResolver>()"));
            Assert.That(source, Does.Not.Contain("CheckPointRulePositioningStrategy"));
            Assert.That(source, Does.Not.Contain("ICheckPointPositioningRecipeCatalog"));
            Assert.That(source, Does.Not.Contain("IRulePositioningStrategy"));
            Assert.That(source, Does.Not.Contain("RulePositioningManager"));
        });
    }

    [Test]
    public void Contracts_RejectNullSuccessValuesAndUseConservativeUnknownDiagnostics()
    {
        Assert.Throws<ArgumentNullException>(() => new CheckPointRecipeSelectionSuccess(null!));
        CheckPointRecipeSelectionFailure unknown = new((CheckPointRecipeSelectionFailureCategory)999);
        Assert.That(unknown.Diagnostic, Is.EqualTo("The Check Point positioning recipe catalog is invalid."));
    }

    [Test]
    public void Constructor_RejectsNullDependencies()
    {
        StrategyFixture fixture = new();
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new CheckPointRulePositioningStrategy(
                null!, fixture.Matrix, fixture.Resolver, fixture.Catalog));
            Assert.Throws<ArgumentNullException>(() => new CheckPointRulePositioningStrategy(
                fixture.Graph, null!, fixture.Resolver, fixture.Catalog));
            Assert.Throws<ArgumentNullException>(() => new CheckPointRulePositioningStrategy(
                fixture.Graph, fixture.Matrix, null!, fixture.Catalog));
            Assert.Throws<ArgumentNullException>(() => new CheckPointRulePositioningStrategy(
                fixture.Graph, fixture.Matrix, fixture.Resolver, null!));
        });
    }

    private static PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> StructuralRecipe() =>
        new(new EndOfBlock(new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer())));

    private static RulePositioningContext ValidContext(
        int managementId = kManagementId,
        int gatewayId = kGatewayId)
    {
        Management management = new() { Id = managementId };
        WfReqTask requestTask = new()
        {
            Id = gatewayId + 100,
            TicketId = gatewayId + 200,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ManagementId = managementId,
            OnManagement = management
        };
        requestTask.SetDeviceList([gatewayId]);
        return new RulePositioningContext(
            new WfImplTask
            {
                Id = gatewayId + 300,
                ReqTaskId = requestTask.Id,
                TicketId = requestTask.TicketId,
                TaskType = WfTaskType.access.ToString(),
                ImplAction = RequestAction.create.ToString(),
                DeviceId = gatewayId,
                ManagementId = managementId
            },
            requestTask,
            new Device
            {
                Id = gatewayId,
                DeviceType = new DeviceType { Id = kCheckPointDeviceTypeId },
                Management = management
            },
            management,
            new GatewayProvisioningSettings { PositioningAlgorithm = kAlgorithm });
    }

    private static RulebaseGraphSnapshot Snapshot(int managementId, int gatewayId, string layerUid)
    {
        Rulebase layer = new()
        {
            Id = gatewayId + 1000,
            MgmtId = managementId,
            Uid = layerUid,
            Name = "Layer",
            Rules = []
        };
        RulebaseLink initial = new()
        {
            GatewayId = gatewayId,
            NextRulebaseId = (int)layer.Id,
            LinkType = RulebaseLinkTypes.Ordered,
            IsInitial = true
        };
        return new RulebaseGraphSnapshot(managementId, gatewayId, [layer], [initial]);
    }

    private static CreationRuleZoneInput ZoneInput() => new(
        kCriterionId,
        [new PositioningZoneReference(1, "zone-1", "Zone", false, false, PositioningZoneKind.Persisted)],
        [new PositioningZoneReference(1, "zone-1", "Zone", false, false, PositioningZoneKind.Persisted)],
        ZoneCoverage.Complete,
        ZoneCoverage.Complete);

    private static RulePositioningContext? InvalidContext(DirectInvalidContext invalid)
    {
        RulePositioningContext context = ValidContext();
        switch (invalid)
        {
            case DirectInvalidContext.NullContext:
                return null;
            case DirectInvalidContext.MissingGateway:
                return context with { TargetGateway = null! };
            case DirectInvalidContext.MissingDeviceType:
                context.TargetGateway.DeviceType = null!;
                break;
            case DirectInvalidContext.MissingManagement:
                return context with { TargetManagement = null! };
            case DirectInvalidContext.MissingRequestTask:
                return context with { ParentRequestTask = null! };
            case DirectInvalidContext.MissingSettings:
                return context with { EffectiveSettings = null! };
            case DirectInvalidContext.InvalidGatewayId:
                context.TargetGateway.Id = 0;
                break;
            case DirectInvalidContext.InvalidManagementId:
                context.TargetManagement.Id = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalid));
        }
        return context;
    }

    private static void AssertFailure(RulePositioningResult result, RulePositioningFailureCategory expected)
    {
        RulePositioningFailure failure = (RulePositioningFailure)result;
        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(expected));
            Assert.That(failure.Diagnostic, Is.EqualTo(new RulePositioningFailure(expected).Diagnostic));
        });
    }

    private static void AssertDownstream(
        StrategyFixture fixture,
        int graph,
        int matrix,
        int resolver,
        int selection,
        int execution)
    {
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Graph.CallCount, Is.EqualTo(graph));
            Assert.That(fixture.Matrix.CallCount, Is.EqualTo(matrix));
            Assert.That(fixture.Resolver.CallCount, Is.EqualTo(resolver));
            Assert.That(fixture.Catalog.SelectCallCount, Is.EqualTo(selection));
            Assert.That(fixture.RootExecutionCount, Is.EqualTo(execution));
        });
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine([directory.FullName, .. pathParts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, pathParts));
    }

    private sealed class StrategyFixture
    {
        public RecordingGraphReader Graph { get; }
        public RecordingMatrixReader Matrix { get; }
        public RecordingZoneResolver Resolver { get; }
        public RecordingCatalog Catalog { get; }
        public CheckPointRulePositioningStrategy Strategy { get; }
        public CheckPointPositioningContext? ExecutionContext { get; set; }
        public CancellationToken ExecutionToken { get; set; }
        public int RootExecutionCount { get; set; }
        public CheckPointRulePosition? ReturnedPosition { get; set; }

        public RulebaseGraphSnapshot Snapshot
        {
            set => Graph.Result = new RulebaseGraphReadSuccess(value);
        }

        public List<ComplianceNetworkZone> Zones
        {
            set => Matrix.Result = new DesignatedZoneMatrixReadSuccess(value);
        }

        public CreationRuleZoneInput ZoneInput
        {
            set => Resolver.Result = new CreationRuleZoneResolutionSuccess(value);
        }

        public StrategyFixture(List<string>? order = null)
        {
            List<string> operationOrder = order ?? [];
            Graph = new RecordingGraphReader(
                new RulebaseGraphReadSuccess(Snapshot(kManagementId, kGatewayId, "layer-default")), operationOrder);
            Matrix = new RecordingMatrixReader(new DesignatedZoneMatrixReadSuccess([]), operationOrder);
            Resolver = new RecordingZoneResolver(new CreationRuleZoneResolutionSuccess(ZoneInput()), operationOrder);
            Catalog = new RecordingCatalog(operationOrder) { Recipe = CreateDefaultRecipe() };
            Strategy = new CheckPointRulePositioningStrategy(Graph, Matrix, Resolver, Catalog);

            PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> CreateDefaultRecipe()
            {
                IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> structural =
                    new EndOfBlock(new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()));
                return new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(
                    new RecordingPassThroughBlock(structural, operationOrder, invocation =>
                    {
                        ExecutionContext = invocation.Context;
                        ExecutionToken = invocation.Token;
                        RootExecutionCount++;
                        ReturnedPosition = ((CheckPointPositionValue)invocation.ChildValues[0]).Position;
                    }));
            }
        }
    }

    public enum DirectInvalidContext
    {
        NullContext,
        MissingGateway,
        MissingDeviceType,
        MissingManagement,
        MissingRequestTask,
        MissingSettings,
        InvalidGatewayId,
        InvalidManagementId
    }

    private sealed class RecordingGraphReader(
        RulebaseGraphReadResult result,
        List<string> order) : IRulebaseGraphSnapshotReader
    {
        public RulebaseGraphReadResult Result { get; set; } = result;
        public Exception? Exception { get; set; }
        public TaskCompletionSource? Pending { get; set; }
        public int CallCount { get; private set; }
        public int ManagementId { get; private set; }
        public int GatewayId { get; private set; }
        public CancellationToken Token { get; private set; }

        public async Task<RulebaseGraphReadResult> ReadAsync(int managementId, int gatewayId, CancellationToken cancellationToken)
        {
            CallCount++;
            ManagementId = managementId;
            GatewayId = gatewayId;
            Token = cancellationToken;
            order.Add("graph");
            if (Exception != null) throw Exception;
            if (Pending != null)
            {
                Pending.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return Result;
        }
    }

    private sealed class RecordingMatrixReader(
        DesignatedZoneMatrixReadResult result,
        List<string>? order = null) : IDesignatedZoneMatrixReader
    {
        public DesignatedZoneMatrixReadResult Result { get; set; } = result;
        public Exception? Exception { get; set; }
        public TaskCompletionSource? Pending { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken Token { get; private set; }

        public async Task<DesignatedZoneMatrixReadResult> ReadAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            Token = cancellationToken;
            order?.Add("matrix");
            if (Exception != null) throw Exception;
            if (Pending != null)
            {
                Pending.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return Result;
        }
    }

    private sealed class RecordingZoneResolver(
        CreationRuleZoneResolutionResult result,
        List<string> order) : ICreationRuleZoneResolver
    {
        public CreationRuleZoneResolutionResult Result { get; set; } = result;
        public Exception? Exception { get; set; }
        public TaskCompletionSource? Pending { get; set; }
        public int CallCount { get; private set; }
        public WfReqTask? RequestTask { get; private set; }
        public IReadOnlyCollection<ComplianceNetworkZone>? Zones { get; private set; }
        public CancellationToken Token { get; private set; }

        public async Task<CreationRuleZoneResolutionResult> ResolveAsync(
            WfReqTask requestTask,
            IReadOnlyCollection<ComplianceNetworkZone> networkZones,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestTask = requestTask;
            Zones = networkZones;
            Token = cancellationToken;
            order.Add("resolve");
            if (Exception != null) throw Exception;
            if (Pending != null)
            {
                Pending.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return Result;
        }
    }

    private sealed class RecordingCatalog(List<string>? order = null) : ICheckPointPositioningRecipeCatalog
    {
        public bool Supports { get; set; } = true;
        public Exception? SupportException { get; set; }
        public Exception? SelectionException { get; set; }
        public Action? SelectionAction { get; set; }
        public CheckPointRecipeSelectionResult? SelectionResult { get; set; }
        public PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? Recipe { get; set; }
        public int SupportCallCount { get; private set; }
        public int SelectCallCount { get; private set; }
        public List<ProvisioningPositioningAlgorithm> SupportedAlgorithms { get; } = [];
        public ProvisioningPositioningAlgorithm SelectedAlgorithm { get; private set; }
        public CreationRuleZoneInput? ZoneInput { get; private set; }

        public bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm)
        {
            SupportCallCount++;
            SupportedAlgorithms.Add(positioningAlgorithm);
            if (SupportException != null) throw SupportException;
            return Supports;
        }

        public CheckPointRecipeSelectionResult SelectRecipe(
            ProvisioningPositioningAlgorithm positioningAlgorithm,
            CreationRuleZoneInput zoneInput)
        {
            SelectCallCount++;
            SelectedAlgorithm = positioningAlgorithm;
            ZoneInput = zoneInput;
            order?.Add("select");
            if (SelectionException != null) throw SelectionException;
            SelectionAction?.Invoke();
            return SelectionResult ?? new CheckPointRecipeSelectionSuccess(Recipe!);
        }
    }

    private sealed class RecordingPassThroughBlock : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
    {
        private static readonly PositioningBlockRole kPositionRole = new("check-point.position");
        private readonly Action<BlockInvocation> onExecute;
        private readonly List<string> order;

        public RecordingPassThroughBlock(
            IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue> child,
            List<string> order,
            Action<BlockInvocation> onExecute)
        {
            Children = [child];
            RequiredChildRoles = [kPositionRole];
            this.order = order;
            this.onExecute = onExecute;
        }

        public string Kind => "test.recording-root";
        public PositioningBlockRole OutputRole => kPositionRole;
        public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children { get; }
        public IReadOnlyList<PositioningBlockRole> RequiredChildRoles { get; }

        public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
            CheckPointPositioningContext context,
            IReadOnlyList<CheckPointBlockValue> childValues,
            CancellationToken cancellationToken)
        {
            order.Add("execute");
            onExecute(new BlockInvocation(context, childValues, cancellationToken));
            return Task.FromResult<PositioningBlockResult<CheckPointBlockValue>>(
                new PositioningBlockSuccess<CheckPointBlockValue>(childValues[0]));
        }
    }

    private sealed record BlockInvocation(
        CheckPointPositioningContext Context,
        IReadOnlyList<CheckPointBlockValue> ChildValues,
        CancellationToken Token);

    private sealed class RecordingResultBlock(
        Func<CheckPointPositioningContext, IReadOnlyList<CheckPointBlockValue>, CancellationToken,
            Task<PositioningBlockResult<CheckPointBlockValue>>> handler)
        : IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>
    {
        private static readonly PositioningBlockRole kRole = new("test.result");
        public int CallCount { get; private set; }
        public string Kind => "test.result";
        public PositioningBlockRole OutputRole => kRole;
        public IReadOnlyList<IPositioningBlock<CheckPointPositioningContext, CheckPointBlockValue>> Children => [];
        public IReadOnlyList<PositioningBlockRole> RequiredChildRoles => [];

        public Task<PositioningBlockResult<CheckPointBlockValue>> EvaluateAsync(
            CheckPointPositioningContext context,
            IReadOnlyList<CheckPointBlockValue> childValues,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return handler(context, childValues, cancellationToken);
        }
    }

    private sealed class DynamicGraphReader(ConcurrentBag<(int ManagementId, int GatewayId)> reads)
        : IRulebaseGraphSnapshotReader
    {
        public Task<RulebaseGraphReadResult> ReadAsync(int managementId, int gatewayId, CancellationToken cancellationToken)
        {
            reads.Add((managementId, gatewayId));
            return Task.FromResult<RulebaseGraphReadResult>(new RulebaseGraphReadSuccess(
                Snapshot(managementId, gatewayId, $"layer-{gatewayId}")));
        }
    }

    private sealed class DynamicZoneResolver : ICreationRuleZoneResolver
    {
        public ConcurrentBag<long> ReceivedTaskIds { get; } = [];

        public Task<CreationRuleZoneResolutionResult> ResolveAsync(
            WfReqTask requestTask,
            IReadOnlyCollection<ComplianceNetworkZone> networkZones,
            CancellationToken cancellationToken)
        {
            ReceivedTaskIds.Add(requestTask.Id);
            return Task.FromResult<CreationRuleZoneResolutionResult>(
                new CreationRuleZoneResolutionSuccess(ZoneInput()));
        }
    }
}
