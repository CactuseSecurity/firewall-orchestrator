using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class CheckPointGraphPositioningTest
{
    private const int kManagementId = 101;
    private const int kGatewayId = 201;
    private const string kLayerUid = "initial-layer";

    [TestCase(ProvisioningPositioningAlgorithm.Undefined)]
    [TestCase(ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair)]
    [TestCase(ProvisioningPositioningAlgorithm.FortinetEndOfZone)]
    [TestCase(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection)]
    [TestCase(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices)]
    [TestCase(ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)]
    [TestCase((ProvisioningPositioningAlgorithm)999)]
    public void Catalog_RejectsEveryUnsupportedAlgorithm(ProvisioningPositioningAlgorithm algorithm)
    {
        CheckPointGraphPositioningRecipeCatalog catalog = new();

        CheckPointGraphRecipeSelectionResult result = catalog.SelectRecipe(algorithm);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.SupportsAlgorithm(algorithm), Is.False);
            Assert.That(result, Is.TypeOf<CheckPointGraphRecipeSelectionFailure>());
            Assert.That(((CheckPointGraphRecipeSelectionFailure)result).Category,
                Is.EqualTo(CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe));
        });
    }

    [Test]
    public void Catalog_ReturnsFreshExactEndOfInitialLayerRecipe()
    {
        CheckPointGraphPositioningRecipeCatalog catalog = new();

        CheckPointGraphRecipeSelectionSuccess first = (CheckPointGraphRecipeSelectionSuccess)
            catalog.SelectRecipe(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);
        CheckPointGraphRecipeSelectionSuccess second = (CheckPointGraphRecipeSelectionSuccess)
            catalog.SelectRecipe(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);

        EndOfBlock root = (EndOfBlock)first.Recipe.Root;
        CheckPointLayerScopeBlock layer = (CheckPointLayerScopeBlock)root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(catalog.SupportsAlgorithm(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase), Is.True);
            Assert.That(second.Recipe, Is.Not.SameAs(first.Recipe));
            Assert.That(layer.Selector.Mode, Is.EqualTo(CheckPointLayerSelectionMode.InitialLayer));
            Assert.That(layer.Selector.Value, Is.Null);
        });
    }

    [TestCase(false, CheckPointPositionAnchorKind.Bottom, null)]
    [TestCase(true, CheckPointPositionAnchorKind.Below, "last-rule")]
    public async Task Strategy_CalculatesEndOfInitialLayer(
        bool populated,
        CheckPointPositionAnchorKind expectedAnchor,
        string? expectedRuleUid)
    {
        RecordingGraphReader reader = new(Snapshot(populated));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(
            Context(),
            CancellationToken.None);

        CheckPointRulePosition position = (CheckPointRulePosition)((RulePositioningSuccess)result).Position;
        Assert.Multiple(() =>
        {
            Assert.That(reader.CallCount, Is.EqualTo(1));
            Assert.That(reader.ManagementId, Is.EqualTo(kManagementId));
            Assert.That(reader.GatewayId, Is.EqualTo(kGatewayId));
            Assert.That(position.LayerUid, Is.EqualTo(kLayerUid));
            Assert.That(position.AnchorKind, Is.EqualTo(expectedAnchor));
            Assert.That(position.AnchorRuleUid, Is.EqualTo(expectedRuleUid));
            Assert.That(position.PositioningAlgorithm,
                Is.EqualTo(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
        });
    }

    [Test]
    public async Task Strategy_RejectsUnsupportedTargetWithoutReadingGraph()
    {
        RecordingGraphReader reader = new(Snapshot(false));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());
        RulePositioningContext context = Context();
        context.TargetGateway.DeviceType.Id = 13;

        RulePositioningResult result = await strategy.CalculateAsync(context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(((RulePositioningFailure)result).Category,
                Is.EqualTo(RulePositioningFailureCategory.PositioningMethodNotImplemented));
            Assert.That(reader.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Strategy_MapsMissingInitialLayerToCalculationFailure()
    {
        Rulebase layer = Layer([]);
        RecordingGraphReader reader = new(new RulebaseGraphSnapshot(kManagementId, kGatewayId, [layer], []));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(Context(), CancellationToken.None);

        Assert.That(((RulePositioningFailure)result).Category,
            Is.EqualTo(RulePositioningFailureCategory.CalculationFailed));
    }

    [Test]
    public async Task Strategy_RejectsInvalidContextBeforeReadingGraph()
    {
        RecordingGraphReader reader = new(Snapshot(false));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(null!, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(((RulePositioningFailure)result).Category,
                Is.EqualTo(RulePositioningFailureCategory.InvalidInput));
            Assert.That(reader.CallCount, Is.Zero);
        });
    }

    [TestCase(RulebaseGraphReadFailureCategory.InvalidInput, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulebaseGraphReadFailureCategory.ScopeMismatch, RulePositioningFailureCategory.InvalidInput)]
    [TestCase(RulebaseGraphReadFailureCategory.InvalidGraphData, RulePositioningFailureCategory.CalculationFailed)]
    public async Task Strategy_MapsGraphFailures(
        RulebaseGraphReadFailureCategory graphCategory,
        RulePositioningFailureCategory expectedCategory)
    {
        RecordingGraphReader reader = new(new RulebaseGraphReadFailure(graphCategory));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(Context(), CancellationToken.None);

        Assert.That(((RulePositioningFailure)result).Category, Is.EqualTo(expectedCategory));
    }

    [TestCase(CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe, RulePositioningFailureCategory.PositioningMethodNotImplemented)]
    [TestCase(CheckPointGraphRecipeSelectionFailureCategory.InvalidRecipe, RulePositioningFailureCategory.ConfigurationInvalid)]
    public async Task Strategy_MapsCatalogFailures(
        CheckPointGraphRecipeSelectionFailureCategory catalogCategory,
        RulePositioningFailureCategory expectedCategory)
    {
        RecordingGraphReader reader = new(Snapshot(false));
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new FailingCatalog(catalogCategory));

        RulePositioningResult result = await strategy.CalculateAsync(Context(), CancellationToken.None);

        Assert.That(((RulePositioningFailure)result).Category, Is.EqualTo(expectedCategory));
    }

    [Test]
    public void Strategy_PropagatesCallerCancellation()
    {
        CheckPointGraphRulePositioningStrategy strategy = new(
            new RecordingGraphReader(Snapshot(false)),
            new CheckPointGraphPositioningRecipeCatalog());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            strategy.CalculateAsync(Context(), cancellation.Token));
    }

    [Test]
    public async Task Strategy_MapsUnexpectedGraphExceptionToCalculationFailure()
    {
        RecordingGraphReader reader = new(Snapshot(false))
        {
            Exception = new InvalidOperationException("sensitive graph failure")
        };
        CheckPointGraphRulePositioningStrategy strategy = new(
            reader,
            new CheckPointGraphPositioningRecipeCatalog());

        RulePositioningResult result = await strategy.CalculateAsync(Context(), CancellationToken.None);

        Assert.That(((RulePositioningFailure)result).Category,
            Is.EqualTo(RulePositioningFailureCategory.CalculationFailed));
    }

    private static RulePositioningContext Context()
    {
        Management management = new()
        {
            Id = kManagementId,
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" }
        };
        Device gateway = new()
        {
            Id = kGatewayId,
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" },
            Management = management
        };
        WfReqTask requestTask = new()
        {
            Id = 301,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            ManagementId = kManagementId,
            OnManagement = management
        };
        requestTask.SetDeviceList([kGatewayId]);
        return new RulePositioningContext(
            new WfImplTask
            {
                Id = 401,
                ReqTaskId = requestTask.Id,
                DeviceId = kGatewayId,
                TaskType = WfTaskType.access.ToString(),
                ImplAction = RequestAction.create.ToString()
            },
            requestTask,
            gateway,
            management,
            new GatewayDeviceSpecificSettings
            {
                PositioningAlgorithm = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase
            });
    }

    private static RulebaseGraphSnapshot Snapshot(bool populated)
    {
        List<Rule> rules = populated
            ? [new Rule { Id = 1001, MgmtId = kManagementId, RulebaseId = 901, Uid = "last-rule", RuleNumNumeric = 1 }]
            : [];
        Rulebase layer = Layer(rules);
        RulebaseLink initial = new()
        {
            GatewayId = kGatewayId,
            NextRulebaseId = (int)layer.Id,
            LinkType = RulebaseLinkTypes.Ordered,
            IsInitial = true
        };
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId, [layer], [initial]);
    }

    private static Rulebase Layer(List<Rule> rules)
    {
        return new Rulebase
        {
            Id = 901,
            MgmtId = kManagementId,
            Uid = kLayerUid,
            Name = "Initial layer",
            Rules = [.. rules]
        };
    }

    private sealed class RecordingGraphReader : IRulebaseGraphSnapshotReader
    {
        private readonly RulebaseGraphReadResult result;

        public int CallCount { get; private set; }
        public int ManagementId { get; private set; }
        public int GatewayId { get; private set; }
        public Exception? Exception { get; set; }

        public RecordingGraphReader(RulebaseGraphSnapshot snapshot)
            : this(new RulebaseGraphReadSuccess(snapshot, 1))
        { }

        public RecordingGraphReader(RulebaseGraphReadResult result)
        {
            this.result = result;
        }

        public Task<RulebaseGraphReadResult> ReadAsync(
            int managementId,
            int gatewayId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            ManagementId = managementId;
            GatewayId = gatewayId;
            if (Exception != null)
            {
                throw Exception;
            }
            return Task.FromResult(result);
        }
    }

    private sealed class FailingCatalog(CheckPointGraphRecipeSelectionFailureCategory category)
        : ICheckPointGraphPositioningRecipeCatalog
    {
        public bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm) => true;

        public CheckPointGraphRecipeSelectionResult SelectRecipe(
            ProvisioningPositioningAlgorithm positioningAlgorithm)
        {
            return new CheckPointGraphRecipeSelectionFailure(category);
        }
    }
}
