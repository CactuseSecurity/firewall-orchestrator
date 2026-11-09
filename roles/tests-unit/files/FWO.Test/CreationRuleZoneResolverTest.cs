using FWO.Compliance.RulePositioning;
using FWO.Data;
using FWO.Data.RulePositioning;
using NetTools;
using NUnit.Framework;
using System.Net;
using CreationRuleZoneResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.CreationRuleZoneInput,
    FWO.Compliance.RulePositioning.CreationRuleZoneResolutionFailureCategory>;

namespace FWO.Test;

[TestFixture]
internal class CreationRuleZoneResolverTest
{
    private const int kCriterionId = 7;

    [Test]
    public async Task ResolveAsync_ResolvesIndependentOrderedZoneSets()
    {
        CreationRuleNetworkInput networkInput = Input(
            [Range("10.0.0.20"), Range("10.0.0.60")],
            [Range("192.0.2.10")]);
        List<NetworkZone> zones =
        [
            Zone(30, "Source broad", "10.0.0.0", "10.0.0.255"),
            Zone(10, "Source narrow", "10.0.0.0", "10.0.0.31"),
            Zone(20, "Destination", "192.0.2.0", "192.0.2.255")
        ];

        CreationRuleZoneInput input = Success(await Resolver().ResolveAsync(
            networkInput, zones, Config(autoCalculateInternetZone: false), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.CriterionId, Is.EqualTo(kCriterionId));
            Assert.That(input.SourceZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 10, 30 }));
            Assert.That(input.DestinationZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 20 }));
            Assert.That(input.SourceCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(input.DestinationCoverage, Is.EqualTo(ZoneCoverage.Complete));
        });
    }

    [Test]
    public async Task ResolveAsync_ReturnsInvalidInputForNullNetworkInput()
    {
        CreationRuleZoneResolutionResult result = await Resolver().ResolveAsync(
            null!,
            [Zone(1, "Zone", "10.0.0.0", "10.0.0.255")],
            Config(autoCalculateInternetZone: false),
            CancellationToken.None);

        Assert.That(Failure(result).Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidInput));
    }

    [Test]
    public async Task ResolveAsync_IdentifiesSyntheticFallbackAndIncompleteIpv6Coverage()
    {
        RulePositioningConfiguration configuration = Config(
            autoCalculateInternetZone: false,
            internetZoneName: "Localized external");
        CreationRuleZoneInput fallbackInput = Success(await Resolver().ResolveAsync(
            Input([Range("203.0.113.1")], [Range("10.0.0.1")]),
            [Zone(1, "Internal", "10.0.0.0", "10.0.0.255")],
            configuration,
            CancellationToken.None));
        CreationRuleZoneInput ipv6Input = Success(await Resolver().ResolveAsync(
            Input([Range("2001:db8::1"), Range("10.0.0.1")], [Range("2001:db8:ffff::1")]),
            [Zone(1, "Internal", "10.0.0.0", "10.0.0.255")],
            configuration,
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(fallbackInput.SourceZones.Single().Id, Is.Null);
            Assert.That(fallbackInput.SourceZones.Single().Kind, Is.EqualTo(PositioningZoneKind.SyntheticInternetFallback));
            Assert.That(fallbackInput.SourceZones.Single().Name, Is.EqualTo("Localized external"));
            Assert.That(ipv6Input.SourceCoverage, Is.EqualTo(ZoneCoverage.Partial));
            Assert.That(ipv6Input.DestinationCoverage, Is.EqualTo(ZoneCoverage.None));
        });
    }

    [Test]
    public async Task ResolveAsync_UsesPersistedAutomaticInternetZone()
    {
        NetworkZone internet = Zone(5, "Internet", "0.0.0.0", "255.255.255.255");
        internet.IsAutoCalculatedInternetZone = true;

        CreationRuleZoneInput input = Success(await Resolver().ResolveAsync(
            Input([Range("203.0.113.1")], [Range("198.51.100.1")]),
            [internet],
            Config(autoCalculateInternetZone: true),
            CancellationToken.None));

        Assert.That(input.SourceZones.Single().Kind, Is.EqualTo(PositioningZoneKind.Persisted));
        Assert.That(input.SourceZones.Single().IsAutoCalculatedInternetZone, Is.True);
    }

    [Test]
    public async Task ResolveAsync_RejectsInvalidZoneCollections()
    {
        CreationRuleNetworkInput input = Input([Range("10.0.0.1")], [Range("10.0.0.2")]);
        NetworkZone malformedZone = Zone(1, "Reversed", "10.0.0.1", "10.0.0.2");
        malformedZone.IPRanges[0].Begin = IPAddress.Parse("10.0.0.3");
        List<IReadOnlyCollection<NetworkZone>> invalidCollections =
        [
            [],
            [Zone(1, "Foreign", "10.0.0.0", "10.0.0.255", kCriterionId + 1)],
            [Zone(1, "First", "10.0.0.0", "10.0.0.127"), Zone(1, "Duplicate", "10.0.0.128", "10.0.0.255")],
            [malformedZone]
        ];

        foreach (IReadOnlyCollection<NetworkZone> zones in invalidCollections)
        {
            FailureValue failure = Failure(await Resolver().ResolveAsync(
                input, zones, Config(autoCalculateInternetZone: false), CancellationToken.None));
            Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidZoneData));
        }
    }

    [Test]
    public async Task ResolveAsync_RequiresExactlyOneValidAutomaticInternetZone()
    {
        CreationRuleNetworkInput input = Input([Range("10.0.0.1")], [Range("10.0.0.2")]);
        RulePositioningConfiguration configuration = Config(autoCalculateInternetZone: true);
        NetworkZone first = Zone(1, "Internet one", "0.0.0.0", "255.255.255.255");
        first.IsAutoCalculatedInternetZone = true;
        NetworkZone second = Zone(2, "Internet two", "192.0.2.0", "192.0.2.255");
        second.IsAutoCalculatedInternetZone = true;
        NetworkZone ipv6Only = Zone(3, "IPv6 Internet", "2001:db8::", "2001:db8::ffff");
        ipv6Only.IsAutoCalculatedInternetZone = true;

        FailureValue missing = Failure(await Resolver().ResolveAsync(
            input, [Zone(4, "Internal", "10.0.0.0", "10.0.0.255")], configuration, CancellationToken.None));
        FailureValue duplicate = Failure(await Resolver().ResolveAsync(
            input, [first, second], configuration, CancellationToken.None));
        FailureValue malformed = Failure(await Resolver().ResolveAsync(
            input, [ipv6Only], configuration, CancellationToken.None));

        Assert.That(new[] { missing.Category, duplicate.Category, malformed.Category },
            Is.All.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidZoneData));
    }

    [Test]
    public async Task ResolveAsync_ReturnsInputUnaffectedByCallerMutation()
    {
        List<NetworkZone> zones =
        [
            Zone(1, "Source", "10.0.0.0", "10.0.0.255"),
            Zone(2, "Destination", "192.0.2.0", "192.0.2.255")
        ];

        CreationRuleZoneInput input = Success(await Resolver().ResolveAsync(
            Input([Range("10.0.0.1")], [Range("192.0.2.1")]),
            zones,
            Config(autoCalculateInternetZone: false),
            CancellationToken.None));
        zones[0].Name = "Mutated";
        zones.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceZones.Single().Name, Is.EqualTo("Source"));
            Assert.That(input.SourceZones, Is.Not.InstanceOf<List<PositioningZoneReference>>());
            Assert.That(input.DestinationZones.Single().Name, Is.EqualTo("Destination"));
        });
    }

    [Test]
    public void ResolveAsync_PropagatesPreCancellation()
    {
        CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await Resolver().ResolveAsync(
            Input([Range("10.0.0.1")], [Range("10.0.0.2")]),
            [Zone(1, "Zone", "10.0.0.0", "10.0.0.255")],
            Config(autoCalculateInternetZone: false),
            cancellation.Token));
    }

    [Test]
    public void ResolveAsync_CancellationDuringZoneEnumeration_Propagates()
    {
        CancellationTokenSource cancellation = new();
        CancellingZoneCollection zones = new(
            [Zone(1, "First", "10.0.0.0", "10.0.0.127"), Zone(2, "Second", "10.0.0.128", "10.0.0.255")],
            cancellation);

        Assert.CatchAsync<OperationCanceledException>(async () => await Resolver().ResolveAsync(
            Input([Range("10.0.0.1")], [Range("10.0.0.2")]),
            zones,
            Config(autoCalculateInternetZone: false),
            cancellation.Token));
    }

    [Test]
    public void ResultContracts_DefensivelyCopyAndUseSafeUnknownDiagnostic()
    {
        List<PositioningZoneReference> source = [new(1, "one", "One", false, false, PositioningZoneKind.Persisted)];
        CreationRuleZoneInput input = new(kCriterionId, source, [], ZoneCoverage.Complete, ZoneCoverage.None);
        source.Clear();
        FailureValue failure = new((CreationRuleZoneResolutionFailureCategory)999);

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceZones, Has.Count.EqualTo(1));
            Assert.That(failure.Diagnostic, Is.EqualTo("The creation-rule zones could not be resolved."));
            Assert.Throws<ArgumentNullException>(() => CreationRuleZoneResolutionResult.Success(null!));
        });
    }

    [Test]
    public async Task ResolveAsync_ReusesExactNormalizedRanges()
    {
        CreationRuleNetworkInput networkInput = Input(
            [Range("10.0.0.10", "10.0.0.20")],
            [Range("192.0.2.10", "192.0.2.20")]);

        CreationRuleZoneInput result = Success(await Resolver().ResolveAsync(
            networkInput,
            [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")],
            Config(autoCalculateInternetZone: false),
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(result.SourceZones.Single().Id, Is.EqualTo(1));
            Assert.That(result.DestinationZones.Single().Id, Is.EqualTo(2));
            Assert.That(result.SourceCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(result.DestinationCoverage, Is.EqualTo(ZoneCoverage.Complete));
        });
    }

    private static CreationRuleZoneResolver Resolver() => new();

    private static RulePositioningConfiguration Config(
        bool autoCalculateInternetZone,
        string internetZoneName = "Internet/Local") =>
        new(kCriterionId, autoCalculateInternetZone, internetZoneName);

    private static CreationRuleNetworkInput Input(
        IReadOnlyCollection<IPAddressRange> sourceRanges,
        IReadOnlyCollection<IPAddressRange> destinationRanges) =>
        new(sourceRanges, destinationRanges);

    private static IPAddressRange Range(string start, string? end = null) =>
        new(IPAddress.Parse(start), IPAddress.Parse(end ?? start));

    private static NetworkZone Zone(
        int id,
        string name,
        string start,
        string end,
        int criterionId = kCriterionId)
    {
        return new NetworkZone
        {
            Id = id,
            IdString = $"zone-{id}",
            Name = name,
            CriterionId = criterionId,
            IPRanges = [Range(start, end)]
        };
    }

    private static CreationRuleZoneInput Success(CreationRuleZoneResolutionResult result)
    {
        Assert.That(result.TryGetValue(out CreationRuleZoneInput? input), Is.True);
        return input!;
    }

    private static FailureValue Failure(CreationRuleZoneResolutionResult result)
    {
        Assert.That(result.TryGetFailure(out CreationRuleZoneResolutionFailureCategory category), Is.True);
        return new FailureValue(category);
    }

    private readonly record struct FailureValue(CreationRuleZoneResolutionFailureCategory Category)
    {
        internal string Diagnostic => Category.GetDiagnostic();
    }

    private sealed class CancellingZoneCollection(
        IReadOnlyList<NetworkZone> zones,
        CancellationTokenSource cancellation) : IReadOnlyCollection<NetworkZone>
    {
        public int Count => zones.Count;

        public IEnumerator<NetworkZone> GetEnumerator()
        {
            for (int index = 0; index < zones.Count; index++)
            {
                if (index == 1)
                {
                    cancellation.Cancel();
                }

                yield return zones[index];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
