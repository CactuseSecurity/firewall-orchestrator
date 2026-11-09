using FWO.Compliance.RulePositioning;
using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointLayerGuardSectionNameResolverTest
{
    private static readonly List<PositioningZoneReference> kNoZones = [];
    private static readonly List<PositioningZoneReference> kSourceZone = [Zone(1, "source")];
    private static readonly List<PositioningZoneReference> kDestinationZone = [Zone(2, "destination")];
    private static readonly List<PositioningZoneReference> kMissingNameAndDestinationZones =
        [Zone(null, ""), Zone(2, "destination")];
    private static readonly List<PositioningZoneReference> kMissingNameAndSourceZones =
        [Zone(null, ""), Zone(1, "source")];
    private static readonly List<PositioningZoneReference> kPlaceholderSourceZone =
        [Zone(1, " @@DESTINATION_ZONE@@ ")];
    private static readonly List<PositioningZoneReference> kStructuralSourceZone = [Zone(1, "[source]<")];
    private static readonly List<PositioningZoneReference> kSeveralSourceZones = [Zone(1, "one"), Zone(2, "two")];
    private static readonly List<PositioningZoneReference> kDuplicateSourceZoneReferences =
        [Zone(1, "source"), Zone(1, "source duplicate")];

    [Test]
    public void Resolve_FixedTemplateNeedsNoZoneInput()
    {
        string value = Resolve(Parse("Birthday section"), null);

        Assert.That(value, Is.EqualTo("BIRTHDAY SECTION"));
    }

    [Test]
    public void Resolve_SourceOnlyIgnoresDestinationCoverageAndCardinality()
    {
        CreationRuleZoneInput zones = Zones(
            kSourceZone,
            kMissingNameAndDestinationZones,
            destinationCoverage: ZoneCoverage.None);

        string value = Resolve(Parse("source=@@SOURCE_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("SOURCE=SOURCE"));
    }

    [Test]
    public void Resolve_DestinationOnlyIgnoresSourceCoverageAndCardinality()
    {
        CreationRuleZoneInput zones = Zones(
            kMissingNameAndSourceZones,
            kDestinationZone,
            sourceCoverage: ZoneCoverage.Partial);

        string value = Resolve(Parse("destination=@@DESTINATION_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("DESTINATION=DESTINATION"));
    }

    [Test]
    public void Resolve_ReusesOneValueForEveryPlaceholderOccurrence()
    {
        List<PositioningZoneReference> sourceZones = [Zone(1, " source ")];
        CreationRuleZoneInput zones = Zones(sourceZones, kNoZones);

        string value = Resolve(Parse("@@SOURCE_ZONE@@/@@SOURCE_ZONE@@/@@SOURCE_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("SOURCE/SOURCE/SOURCE"));
    }

    [Test]
    public void Resolve_DoesNotInterpretPlaceholderTextInsideAZoneName()
    {
        CreationRuleZoneInput zones = Zones(kPlaceholderSourceZone, kNoZones);

        string value = Resolve(Parse("@@SOURCE_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("@@DESTINATION_ZONE@@"));
    }

    [Test]
    public void Resolve_AllowsFormerlyStructuralCharactersInAZoneName()
    {
        CreationRuleZoneInput zones = Zones(kStructuralSourceZone, kNoZones);

        string value = Resolve(Parse("zone=@@SOURCE_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("ZONE=[SOURCE]<"));
    }

    [TestCase(ZoneCoverage.Partial)]
    [TestCase(ZoneCoverage.None)]
    public void Resolve_RejectsIncompleteCoverageOnReferencedSide(ZoneCoverage coverage)
    {
        CreationRuleZoneInput zones = Zones(kSourceZone, kNoZones, sourceCoverage: coverage);

        AssertFailure(Parse("@@SOURCE_ZONE@@"), zones, CheckPointLayerGuardSectionNameFailureCategory.ZoneCoverageInvalid);
    }

    [Test]
    public void Resolve_RejectsMissingZoneOnReferencedSide()
    {
        AssertFailure(Parse("@@SOURCE_ZONE@@"), Zones(kNoZones, kNoZones),
            CheckPointLayerGuardSectionNameFailureCategory.ZoneCardinalityInvalid);
    }

    [Test]
    public void Resolve_RejectsSeveralDistinctZonesOnReferencedSide()
    {
        AssertFailure(Parse("@@SOURCE_ZONE@@"), Zones(kSeveralSourceZones, kNoZones),
            CheckPointLayerGuardSectionNameFailureCategory.ZoneCardinalityInvalid);
    }

    [Test]
    public void Resolve_DeduplicatesRepeatedReferencesToTheSameZone()
    {
        CreationRuleZoneInput zones = Zones(kDuplicateSourceZoneReferences, kNoZones);

        string value = Resolve(Parse("@@SOURCE_ZONE@@"), zones);

        Assert.That(value, Is.EqualTo("SOURCE"));
    }

    [TestCase(null, PositioningZoneKind.SyntheticInternetFallback)]
    [TestCase(0, PositioningZoneKind.Persisted)]
    [TestCase(-1, PositioningZoneKind.Persisted)]
    [TestCase(1, PositioningZoneKind.SyntheticInternetFallback)]
    public void Resolve_RejectsNonPersistedZoneOnReferencedSide(int? id, PositioningZoneKind kind)
    {
        List<PositioningZoneReference> sourceZones = [Zone(id, "source", kind)];
        AssertFailure(Parse("@@SOURCE_ZONE@@"), Zones(sourceZones, kNoZones),
            CheckPointLayerGuardSectionNameFailureCategory.ZoneNotPersisted);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Resolve_RejectsEmptyNameOnReferencedSide(string name)
    {
        List<PositioningZoneReference> sourceZones = [Zone(1, name)];
        AssertFailure(Parse("@@SOURCE_ZONE@@"), Zones(sourceZones, kNoZones),
            CheckPointLayerGuardSectionNameFailureCategory.ZoneDataInvalid);
    }

    [Test]
    public void Resolve_RejectsMissingZoneInputForReferencedVariable()
    {
        AssertFailure(Parse("@@SOURCE_ZONE@@"), null,
            CheckPointLayerGuardSectionNameFailureCategory.MissingZoneInput);
    }

    private static CheckPointSectionNameTemplate Parse(string text)
    {
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> result =
            CheckPointSectionNameTemplate.Parse(text);
        Assert.That(result.TryGetValue(out CheckPointSectionNameTemplate? template), Is.True);
        return template!;
    }

    private static string Resolve(CheckPointSectionNameTemplate template, CreationRuleZoneInput? zones)
    {
        PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> result =
            CheckPointLayerGuardSectionNameResolver.Resolve(template, zones);
        Assert.That(result.TryGetValue(out string? value), Is.True);
        return value!;
    }

    private static void AssertFailure(
        CheckPointSectionNameTemplate template,
        CreationRuleZoneInput? zones,
        CheckPointLayerGuardSectionNameFailureCategory expected)
    {
        PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> result =
            CheckPointLayerGuardSectionNameResolver.Resolve(template, zones);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TryGetValue(out _), Is.False);
            Assert.That(result.TryGetFailure(out CheckPointLayerGuardSectionNameFailureCategory failure), Is.True);
            Assert.That(failure, Is.EqualTo(expected));
        }
    }

    private static CreationRuleZoneInput Zones(
        IReadOnlyCollection<PositioningZoneReference> source,
        IReadOnlyCollection<PositioningZoneReference> destination,
        ZoneCoverage sourceCoverage = ZoneCoverage.Complete,
        ZoneCoverage destinationCoverage = ZoneCoverage.Complete) =>
        new(1, source, destination, sourceCoverage, destinationCoverage);

    private static PositioningZoneReference Zone(
        int? id,
        string name,
        PositioningZoneKind kind = PositioningZoneKind.Persisted) =>
        new(id, id?.ToString() ?? "synthetic", name, false, kind == PositioningZoneKind.SyntheticInternetFallback, kind);
}
