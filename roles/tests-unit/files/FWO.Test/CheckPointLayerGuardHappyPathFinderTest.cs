using System.Net;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services.RulePositioning.CheckPoint;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class CheckPointLayerGuardHappyPathFinderTest
{
    private const int kManagementId = 10;
    private const int kGatewayId = 20;
    private const long kImportId = 30;
    private const ProvisioningPositioningAlgorithm kAlgorithm =
        ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection;

    [Test]
    public async Task FindAsync_ReturnsFirstProductionValidatedZonePairCandidate()
    {
        RulebaseGraphSnapshot graph = Graph("[Source] -> [Destination]", includeSectionRule: true);
        CheckPointLayerGuardSnapshot guards = new(
            kManagementId,
            kGatewayId,
            kImportId,
            [Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24")]);
        List<NetworkZone> zones =
        [
            Zone(1, "Source", "10.0.0.0/24"),
            Zone(2, "Destination", "20.0.0.0/24")
        ];
        RulePositioningConfiguration configuration = new(40, true, "Internet");
        CheckPointSectionNameTemplate template = Template(
            CheckPointLayerGuardSettingDefaults.kSectionNameTemplate);

        CheckPointLayerGuardHappyPathSearchResult result = await new CheckPointLayerGuardHappyPathFinder()
            .FindAsync(Context(configuration), graph, guards, zones, configuration, template, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate, Is.Not.Null);
            Assert.That(result.Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("10.0.0.0")));
            Assert.That(result.Candidate.DestinationAddress, Is.EqualTo(IPAddress.Parse("20.0.0.0")));
            Assert.That(result.Candidate.SourceZoneId, Is.EqualTo(1));
            Assert.That(result.Candidate.DestinationZoneId, Is.EqualTo(2));
            Assert.That(result.Candidate.GuardRuleId, Is.EqualTo(10));
            Assert.That(result.Candidate.TargetLayerId, Is.EqualTo(2));
            Assert.That(result.Candidate.TargetLayerUid, Is.EqualTo("inline"));
            Assert.That(result.Candidate.SectionId, Is.EqualTo(3));
            Assert.That(result.Candidate.SectionUid, Is.EqualTo("section"));
            Assert.That(result.Candidate.RenderedSectionName, Is.EqualTo("[SOURCE] -> [DESTINATION]"));
            Assert.That(result.Candidate.PredictedPosition.AnchorKind,
                Is.EqualTo(CheckPointPositionAnchorKind.Below));
            Assert.That(result.Candidate.PredictedPosition.AnchorRuleUid, Is.EqualTo("section-rule"));
        });
    }

    [Test]
    public async Task FindAsync_SkipsAmbiguousOverlapAndUsesLaterUniqueInterval()
    {
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24")],
            [
                Zone(1, "Source", "10.0.0.0/24"),
                Zone(2, "Destination", "20.0.0.0/24"),
                Zone(3, "Overlap", "10.0.0.0-10.0.0.9")
            ]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("10.0.0.10")));
            Assert.That(result.Statistics.ZoneRejectionCount, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task FindAsync_RespectsPositiveExclusionAndNegatedGuard()
    {
        CheckPointLayerGuard guard = new(
            10,
            1,
            2,
            new CheckPointGuardSide(false,
                [new CheckPointGuardAddressTerm(
                    [Range("10.0.0.0-10.0.0.20")], [Range("10.0.0.0-10.0.0.9")], false, false)]),
            new CheckPointGuardSide(true,
                [new CheckPointGuardAddressTerm([Range("20.0.0.0-20.0.0.9")], [], false, false)]));
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [guard],
            [Zone(1, "Source", "10.0.0.0-10.0.0.20"), Zone(2, "Destination", "20.0.0.0-20.0.0.20")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("10.0.0.10")));
            Assert.That(result.Candidate.DestinationAddress, Is.EqualTo(IPAddress.Parse("20.0.0.10")));
        });
    }

    [Test]
    public async Task FindAsync_ReturnsIndeterminateWhenUnresolvedTermsPreventProof()
    {
        CheckPointLayerGuard guard = new(
            10,
            1,
            2,
            new CheckPointGuardSide(false, [new CheckPointGuardAddressTerm([], [], true, false)]),
            KnownSide("20.0.0.0/24"));
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [guard],
            [Zone(1, "Source", "10.0.0.1"), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Indeterminate));
            Assert.That(result.Reason, Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.GuardIndeterminate));
            Assert.That(result.Candidate, Is.Null);
        });
    }

    [Test]
    public async Task FindAsync_TwoDefiniteGuardsReturnNoCandidate()
    {
        RulebaseGraphSnapshot graph = GraphWithTwoInlineLayers("[Source] -> [Destination]");
        SearchFixture fixture = Fixture(
            graph,
            [
                Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"),
                Guard(11, 1, 4, "10.0.0.0/24", "20.0.0.0/24")
            ],
            [Zone(1, "Source", "10.0.0.1"), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.NoCandidate));
    }

    [Test]
    public async Task FindAsync_UsesCompetingGuardBoundariesToFindLaterUniqueMatch()
    {
        SearchFixture fixture = Fixture(
            GraphWithTwoInlineLayers("[Source] -> [Destination]"),
            [
                Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"),
                Guard(11, 1, 4, "10.0.0.0/25", "20.0.0.0/24")
            ],
            [Zone(1, "Source", "10.0.0.0/24"), Zone(2, "Destination", "20.0.0.0/24")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("10.0.0.128")));
            Assert.That(result.Candidate.GuardRuleId, Is.EqualTo(10));
            Assert.That(result.Candidate.TargetLayerId, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task FindAsync_UnresolvedCompetingGuardOutsideKnownRangeRemainsIndeterminate()
    {
        CheckPointLayerGuard unresolvedCompetitor = new(
            11,
            1,
            4,
            new CheckPointGuardSide(false,
                [new CheckPointGuardAddressTerm([Range("10.0.0.0/25")], [], true, false)]),
            KnownSide("20.0.0.0/24"));
        SearchFixture fixture = Fixture(
            GraphWithTwoInlineLayers("[Source] -> [Destination]"),
            [Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24"), unresolvedCompetitor],
            [Zone(1, "Source", "10.0.0.0/24"), Zone(2, "Destination", "20.0.0.0/24")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Indeterminate));
    }

    [Test]
    public async Task FindAsync_DuplicateNormalizedSectionsAreRejectedAsAmbiguous()
    {
        SearchFixture fixture = Fixture(
            GraphWithDuplicateSections(" [Source] -> [Destination] ", "[SOURCE] -> [DESTINATION]"),
            [Guard(10, 1, 2, "10.0.0.1", "20.0.0.1")],
            [Zone(1, "Source", "10.0.0.1"), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.NoCandidate));
            Assert.That(result.Statistics.SectionRejectionCount, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task FindAsync_RendersZonePairsOnceAcrossRealisticGuardCardinality()
    {
        List<NetworkZone> zones = Enumerable.Range(1, 60)
            .Select(id => Zone(id, $"Zone-{id}", $"10.0.{id}.1"))
            .ToList();
        List<CheckPointLayerGuard> guards = Enumerable.Range(0, 59)
            .Select(index => Guard(
                1_000 + index,
                1,
                2 + index * 2,
                index == 58 ? "10.0.59.1" : "10.0.1.1",
                index == 58 ? "10.0.60.1" : "10.0.1.1"))
            .ToList();
        SearchFixture fixture = Fixture(
            GraphWithInlineLayers(59, "[Zone-59] -> [Zone-60]"),
            guards,
            zones);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Reason, Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.CandidateValidated));
            Assert.That(result.Statistics.RenderedZonePairCount, Is.EqualTo(3_600));
            Assert.That(result.Statistics.RenderedZonePairCount, Is.LessThan(10_000));
            Assert.That(result.Statistics.StructuralMatchCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.Statistics.SectionRejectionCount, Is.EqualTo(3_599));
            Assert.That(result.Candidate?.TargetLayerId, Is.EqualTo(118));
        });
    }

    [Test]
    public async Task FindAsync_MoreThanTenThousandUniqueZonePairsReturnsSearchLimitReached()
    {
        List<NetworkZone> zones = Enumerable.Range(1, 101)
            .Select(id => Zone(id, $"Zone-{id}", $"10.0.{id}.1"))
            .ToList();
        SearchFixture fixture = Fixture(
            Graph("Missing", includeSectionRule: true),
            [Guard(10, 1, 2, "10.0.1.1", "10.0.2.1")],
            zones);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached));
            Assert.That(result.Reason,
                Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.RenderedZonePairLimit));
            Assert.That(result.Statistics.RenderedZonePairCount, Is.EqualTo(10_000));
        });
    }

    [Test]
    public async Task FindAsync_SharedZoneNamesTryMatchingPairsInZoneIdOrder()
    {
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [Guard(10, 1, 2, "10.0.0.1-10.0.0.2", "20.0.0.1-20.0.0.2")],
            [
                Zone(4, "Destination", "20.0.0.2"),
                Zone(2, "Source", "10.0.0.2"),
                Zone(3, "Destination", "20.0.0.1"),
                Zone(1, "Source", "10.0.0.1")
            ]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Statistics.StructuralMatchCount, Is.EqualTo(4));
            Assert.That(result.Candidate!.SourceZoneId, Is.EqualTo(1));
            Assert.That(result.Candidate.DestinationZoneId, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task FindAsync_MissingRenderedSectionReturnsNoCandidateAfterExhaustion()
    {
        SearchFixture fixture = Fixture(
            Graph("Different", includeSectionRule: true),
            [Guard(10, 1, 2, "10.0.0.1", "20.0.0.1")],
            [Zone(1, "Source", "10.0.0.1"), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.NoCandidate));
    }

    [Test]
    public async Task FindAsync_UnrelatedEmptyZoneDoesNotInvalidateSearch()
    {
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [Guard(10, 1, 2, "10.0.0.1", "20.0.0.1")],
            [
                Zone(1, "Source", "10.0.0.1"),
                Zone(2, "Destination", "20.0.0.1"),
                new NetworkZone { Id = 3, Name = "Empty", IPRanges = [] }
            ]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
    }

    [Test]
    public void PartitionAddresses_StopsAsSoonAsRepresentativeLimitIsExceeded()
    {
        NetworkZone target = new()
        {
            Id = 1,
            Name = "Fragmented",
            IPRanges = [Range("10.0.0.1"), Range("10.0.0.3"), Range("10.0.0.5")]
        };

        CheckPointRepresentativeAddressPartition partition =
            CheckPointLayerGuardHappyPathFinder.PartitionAddresses(
                target,
                [target],
                [KnownSide("10.0.0.0/24")],
                maximumAddresses: 1,
                CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(partition.LimitReached, Is.True);
            Assert.That(partition.Addresses, Has.Count.EqualTo(1));
            Assert.That(partition.TargetRangeCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task FindAsync_Ipv6SingleAddressAtFamilyMaximumDoesNotOverflow()
    {
        const string maximumIpv6 = "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff";
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: true),
            [Guard(10, 1, 2, maximumIpv6, "20.0.0.1")],
            [Zone(1, "Source", maximumIpv6), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse(maximumIpv6)));
        });
    }

    [Test]
    public async Task FindAsync_EmptyMatchingSectionReturnsCanonicalTopBoundary()
    {
        SearchFixture fixture = Fixture(
            Graph("[Source] -> [Destination]", includeSectionRule: false),
            [Guard(10, 1, 2, "10.0.0.1", "20.0.0.1")],
            [Zone(1, "Source", "10.0.0.1"), Zone(2, "Destination", "20.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult result = await Find(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(result.Candidate!.PredictedPosition.AnchorKind, Is.EqualTo(CheckPointPositionAnchorKind.Top));
            Assert.That(result.Candidate.PredictedPosition.AnchorRuleUid, Is.Null);
        });
    }

    [Test]
    public void FindAsync_PreCancelledTokenThrowsOperationCanceledException()
    {
        SearchFixture fixture = DefaultFixture();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await Find(fixture, cancellation.Token));
    }

    [Test]
    public void FindAsync_CancellationDuringLargeSearchThrowsOperationCanceledException()
    {
        RulebaseGraphSnapshot graph = Graph("Missing", includeSectionRule: true);
        List<NetworkZone> zones = Enumerable.Range(1, 300)
            .Select(id => Zone(id, $"Zone-{id}", $"10.{id / 256}.{id % 256}.1"))
            .ToList();
        SearchFixture fixture = Fixture(graph, [Guard(10, 1, 2, "0.0.0.0/0", "0.0.0.0/0")], zones);
        using CancellationTokenSource cancellation = new();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(5));

        Assert.ThrowsAsync<OperationCanceledException>(async () => await Task.Run(
            async () => await new CheckPointLayerGuardHappyPathFinder(100_000, 4_096, 100_000)
                .FindAsync(fixture.Context, fixture.Graph, fixture.Guards, fixture.Zones,
                    fixture.Configuration, fixture.Template, cancellation.Token)));
    }

    [Test]
    public async Task FindAsync_EachExplicitBoundReturnsSearchLimitReached()
    {
        SearchFixture ordinary = DefaultFixture();
        SearchFixture overlap = Fixture(
            ordinary.Graph,
            ordinary.Guards.GuardsByTargetLayerId.Values,
            [
                Zone(1, "Source", "10.0.0.0/24"),
                Zone(2, "Destination", "20.0.0.1"),
                Zone(3, "Overlap", "10.0.0.0-10.0.0.9")
            ]);

        CheckPointLayerGuardHappyPathSearchResult zonePairs = await Find(
            ordinary, finder: new CheckPointLayerGuardHappyPathFinder(1, 4_096, 100_000));
        CheckPointLayerGuardHappyPathSearchResult representatives = await Find(
            overlap, finder: new CheckPointLayerGuardHappyPathFinder(10_000, 1, 100_000));
        CheckPointLayerGuardHappyPathSearchResult addressPairs = await Find(
            overlap, finder: new CheckPointLayerGuardHappyPathFinder(10_000, 4_096, 1));

        Assert.Multiple(() =>
        {
            Assert.That(zonePairs.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached));
            Assert.That(zonePairs.Reason, Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.RenderedZonePairLimit));
            Assert.That(representatives.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached));
            Assert.That(representatives.Reason,
                Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.RepresentativeAddressLimit));
            Assert.That(addressPairs.Status, Is.EqualTo(CheckPointLayerGuardHappyPathSearchStatus.SearchLimitReached));
            Assert.That(addressPairs.Reason, Is.EqualTo(CheckPointLayerGuardHappyPathSearchReason.AddressPairLimit));
        });
    }

    [Test]
    public async Task FindAsync_ConcurrentCallsDoNotShareSearchState()
    {
        CheckPointLayerGuardHappyPathFinder finder = new();
        SearchFixture first = DefaultFixture();
        SearchFixture second = Fixture(
            Graph("[Other Source] -> [Other Destination]", includeSectionRule: true),
            [Guard(10, 1, 2, "30.0.0.1", "40.0.0.1")],
            [Zone(11, "Other Source", "30.0.0.1"), Zone(12, "Other Destination", "40.0.0.1")]);

        CheckPointLayerGuardHappyPathSearchResult[] results = await Task.WhenAll(
            Find(first, finder: finder),
            Find(second, finder: finder));

        Assert.Multiple(() =>
        {
            Assert.That(results.Select(result => result.Status), Is.All.EqualTo(
                CheckPointLayerGuardHappyPathSearchStatus.Found));
            Assert.That(results[0].Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("10.0.0.0")));
            Assert.That(results[1].Candidate!.SourceAddress, Is.EqualTo(IPAddress.Parse("30.0.0.1")));
            Assert.That(results[0].Statistics.AddressPairValidationCount, Is.EqualTo(1));
            Assert.That(results[1].Statistics.AddressPairValidationCount, Is.EqualTo(1));
        });
    }

    private static SearchFixture DefaultFixture() => Fixture(
        Graph("[Source] -> [Destination]", includeSectionRule: true),
        [Guard(10, 1, 2, "10.0.0.0/24", "20.0.0.0/24")],
        [Zone(1, "Source", "10.0.0.0/24"), Zone(2, "Destination", "20.0.0.0/24")]);

    private static SearchFixture Fixture(
        RulebaseGraphSnapshot graph,
        IEnumerable<CheckPointLayerGuard> guards,
        IReadOnlyCollection<NetworkZone> zones)
    {
        RulePositioningConfiguration configuration = new(40, true, "Internet");
        return new SearchFixture(
            Context(configuration),
            graph,
            new CheckPointLayerGuardSnapshot(kManagementId, kGatewayId, kImportId, guards.ToList()),
            zones,
            configuration,
            Template(CheckPointLayerGuardSettingDefaults.kSectionNameTemplate));
    }

    private static Task<CheckPointLayerGuardHappyPathSearchResult> Find(
        SearchFixture fixture,
        CancellationToken cancellationToken = default,
        CheckPointLayerGuardHappyPathFinder? finder = null) =>
        (finder ?? new CheckPointLayerGuardHappyPathFinder()).FindAsync(
            fixture.Context,
            fixture.Graph,
            fixture.Guards,
            fixture.Zones,
            fixture.Configuration,
            fixture.Template,
            cancellationToken);

    private static CheckPointSectionNameTemplate Template(string value)
    {
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> result =
            CheckPointSectionNameTemplate.Parse(value);
        Assert.That(result.TryGetValue(out CheckPointSectionNameTemplate? template), Is.True);
        return template!;
    }

    private static RulePositioningContext Context(RulePositioningConfiguration configuration) => new(
        new WfImplTask(),
        new WfReqTask(),
        new Device { Id = kGatewayId },
        new Management { Id = kManagementId },
        new GatewayDeviceSpecificSettings { PositioningAlgorithm = kAlgorithm },
        configuration: configuration);

    private static NetworkZone Zone(int id, string name, string range) => new()
    {
        Id = id,
        Name = name,
        IPRanges = [Range(range)]
    };

    private static CheckPointLayerGuard Guard(
        long sourceRuleId,
        long sourceRulebaseId,
        long targetLayerId,
        string source,
        string destination) => new(
            sourceRuleId,
            sourceRulebaseId,
            targetLayerId,
            KnownSide(source),
            KnownSide(destination));

    private static CheckPointGuardSide KnownSide(string range) => new(false,
        [new CheckPointGuardAddressTerm([Range(range)], [], false, false)]);

    private static RulebaseGraphSnapshot Graph(string sectionName, bool includeSectionRule)
    {
        Rulebase main = Rulebase(1, "main", "Main", Rule(10, "guard-rule", 1));
        Rulebase inline = Rulebase(2, "inline", "Inline");
        Rulebase section = includeSectionRule
            ? Rulebase(3, "section", sectionName, Rule(30, "section-rule", 1))
            : Rulebase(3, "section", sectionName);
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId, [main, inline, section],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline),
            Link(2, null, 3, RulebaseLinkTypes.Concatenated, section: true)
        ]);
    }

    private static RulebaseGraphSnapshot GraphWithTwoInlineLayers(string sectionName)
    {
        Rulebase main = Rulebase(1, "main", "Main", Rule(10, "guard-one", 1), Rule(11, "guard-two", 2));
        Rulebase first = Rulebase(2, "inline-one", "Inline One");
        Rulebase firstSection = Rulebase(3, "section-one", sectionName, Rule(30, "section-one-rule", 1));
        Rulebase second = Rulebase(4, "inline-two", "Inline Two");
        Rulebase secondSection = Rulebase(5, "section-two", sectionName, Rule(50, "section-two-rule", 1));
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId,
            [main, first, firstSection, second, secondSection],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline),
            Link(2, null, 3, RulebaseLinkTypes.Concatenated, section: true),
            Link(1, 11, 4, RulebaseLinkTypes.Inline),
            Link(4, null, 5, RulebaseLinkTypes.Concatenated, section: true)
        ]);
    }

    private static RulebaseGraphSnapshot GraphWithInlineLayers(int layerCount, string finalSectionName)
    {
        List<Rule> guardRules = Enumerable.Range(0, layerCount)
            .Select(index => Rule(1_000 + index, $"guard-{index}", index + 1))
            .ToList();
        List<Rulebase> rulebases = [Rulebase(1, "main", "Main", guardRules.ToArray())];
        List<RulebaseLink> links = [Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true)];
        for (int index = 0; index < layerCount; index++)
        {
            int layerId = 2 + index * 2;
            int sectionId = layerId + 1;
            string sectionName = index == layerCount - 1 ? finalSectionName : $"Unmatched-{index}";
            rulebases.Add(Rulebase(layerId, $"inline-{index}", $"Inline {index}"));
            rulebases.Add(Rulebase(sectionId, $"section-{index}", sectionName,
                Rule(2_000 + index, $"section-rule-{index}", 1)));
            links.Add(Link(1, 1_000 + index, layerId, RulebaseLinkTypes.Inline));
            links.Add(Link(layerId, null, sectionId, RulebaseLinkTypes.Concatenated, section: true));
        }
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId, rulebases, links);
    }

    private static RulebaseGraphSnapshot GraphWithDuplicateSections(string firstName, string secondName)
    {
        Rulebase main = Rulebase(1, "main", "Main", Rule(10, "guard-rule", 1));
        Rulebase inline = Rulebase(2, "inline", "Inline");
        Rulebase first = Rulebase(3, "section-one", firstName, Rule(30, "first-rule", 1));
        Rulebase second = Rulebase(4, "section-two", secondName, Rule(40, "second-rule", 2));
        return new RulebaseGraphSnapshot(kManagementId, kGatewayId, [main, inline, first, second],
        [
            Link(null, null, 1, RulebaseLinkTypes.Ordered, initial: true),
            Link(1, 10, 2, RulebaseLinkTypes.Inline),
            Link(2, null, 3, RulebaseLinkTypes.Concatenated, section: true),
            Link(3, null, 4, RulebaseLinkTypes.Concatenated, section: true)
        ]);
    }

    private static Rulebase Rulebase(int id, string uid, string name, params Rule[] rules)
    {
        foreach (Rule rule in rules)
        {
            rule.MgmtId = kManagementId;
            rule.RulebaseId = id;
        }
        return new Rulebase { Id = id, Uid = uid, Name = name, MgmtId = kManagementId, Rules = rules };
    }

    private static Rule Rule(int id, string uid, double order) =>
        new() { Id = id, Uid = uid, RuleNumNumeric = order };

    private static RulebaseLink Link(
        int? sourceRulebaseId,
        int? sourceRuleId,
        int targetRulebaseId,
        int linkType,
        bool initial = false,
        bool section = false) => new()
        {
            GatewayId = kGatewayId,
            FromRulebaseId = sourceRulebaseId,
            FromRuleId = sourceRuleId,
            NextRulebaseId = targetRulebaseId,
            LinkType = linkType,
            IsInitial = initial,
            IsSection = section
        };

    private static IPAddressRange Range(string value) => IPAddressRange.Parse(value);

    private sealed record SearchFixture(
        RulePositioningContext Context,
        RulebaseGraphSnapshot Graph,
        CheckPointLayerGuardSnapshot Guards,
        IReadOnlyCollection<NetworkZone> Zones,
        RulePositioningConfiguration Configuration,
        CheckPointSectionNameTemplate Template);
}
