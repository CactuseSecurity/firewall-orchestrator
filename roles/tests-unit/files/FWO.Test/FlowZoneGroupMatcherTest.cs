using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests parsing, normalizing and applying the configured flow zone group name patterns.
/// </summary>
[TestFixture]
internal class FlowZoneGroupMatcherTest
{
    private static readonly List<FlowZoneGroupPattern> kNoPatterns = [];

    private static readonly List<FlowZoneGroupPattern> kSuffixPatterns =
    [
        new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Suffix, CaseSensitive = false, Value = "_zone" },
        new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Suffix, CaseSensitive = true, Value = "-zone" }
    ];

    [Test]
    public void ParsePatterns_WithEmptyOrInvalidValue_ReturnsEmptyList()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowZoneGroupMatcher.ParsePatterns(null), Is.Empty);
            Assert.That(FlowZoneGroupMatcher.ParsePatterns(""), Is.Empty);
            Assert.That(FlowZoneGroupMatcher.ParsePatterns("   "), Is.Empty);
            Assert.That(FlowZoneGroupMatcher.ParsePatterns("[]"), Is.Empty);
            Assert.That(FlowZoneGroupMatcher.ParsePatterns("not json"), Is.Empty);
        });
    }

    [Test]
    public void ParsePatterns_WithConfiguredPatterns_KeepsOrderAndValues()
    {
        const string serializedPatterns =
            "[{\"matchType\":\"Suffix\",\"caseSensitive\":false,\"value\":\"_zone\"},{\"matchType\":\"Prefix\",\"caseSensitive\":true,\"value\":\"zone-\"}]";

        List<FlowZoneGroupPattern> patterns = FlowZoneGroupMatcher.ParsePatterns(serializedPatterns);

        Assert.That(patterns, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(patterns[0].MatchType, Is.EqualTo(FlowZoneNameMatchType.Suffix));
            Assert.That(patterns[0].CaseSensitive, Is.False);
            Assert.That(patterns[0].Value, Is.EqualTo("_zone"));
            Assert.That(patterns[1].MatchType, Is.EqualTo(FlowZoneNameMatchType.Prefix));
            Assert.That(patterns[1].CaseSensitive, Is.True);
            Assert.That(patterns[1].Value, Is.EqualTo("zone-"));
        });
    }

    [Test]
    public void ParsePatterns_WithUnknownMatchType_ReturnsEmptyList()
    {
        List<FlowZoneGroupPattern> patterns =
            FlowZoneGroupMatcher.ParsePatterns("[{\"matchType\":\"Regex\",\"caseSensitive\":false,\"value\":\"_zone\"}]");

        Assert.That(patterns, Is.Empty);
    }

    [Test]
    public void ParsePatterns_WithMissingMatchType_FallsBackToSuffix()
    {
        List<FlowZoneGroupPattern> patterns = FlowZoneGroupMatcher.ParsePatterns("[{\"value\":\"_zone\"}]");

        Assert.That(patterns, Has.Count.EqualTo(1));
        Assert.That(patterns[0].MatchType, Is.EqualTo(FlowZoneNameMatchType.Suffix));
    }

    [Test]
    public void Normalize_DropsEmptyValuesAndDuplicatesAndTrims()
    {
        List<FlowZoneGroupPattern> sourcePatterns =
        [
            new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Suffix, Value = "  _zone  " },
            new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Suffix, Value = "_ZONE" },
            new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Suffix, CaseSensitive = true, Value = "_ZONE" },
            new FlowZoneGroupPattern { MatchType = FlowZoneNameMatchType.Prefix, Value = "   " },
            new FlowZoneGroupPattern { MatchType = (FlowZoneNameMatchType)42, Value = "_zone" }
        ];

        List<FlowZoneGroupPattern> normalizedPatterns = FlowZoneGroupMatcher.Normalize(sourcePatterns);

        Assert.That(normalizedPatterns, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(normalizedPatterns[0].Value, Is.EqualTo("_zone"));
            Assert.That(normalizedPatterns[0].CaseSensitive, Is.False);
            Assert.That(normalizedPatterns[1].Value, Is.EqualTo("_ZONE"));
            Assert.That(normalizedPatterns[1].CaseSensitive, Is.True);
        });
    }

    [Test]
    public void Normalize_WithNullInput_ReturnsEmptyList()
    {
        Assert.That(FlowZoneGroupMatcher.Normalize(null), Is.Empty);
    }

    [Test]
    public void SerializePatterns_RoundTripsThroughParsePatterns()
    {
        string serializedPatterns = FlowZoneGroupMatcher.SerializePatterns(kSuffixPatterns);
        List<FlowZoneGroupPattern> parsedPatterns = FlowZoneGroupMatcher.ParsePatterns(serializedPatterns);

        Assert.That(serializedPatterns, Does.Contain("\"matchType\":\"Suffix\""));
        Assert.That(parsedPatterns, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(parsedPatterns[0].Value, Is.EqualTo("_zone"));
            Assert.That(parsedPatterns[0].CaseSensitive, Is.False);
            Assert.That(parsedPatterns[1].Value, Is.EqualTo("-zone"));
            Assert.That(parsedPatterns[1].CaseSensitive, Is.True);
        });
    }

    [Test]
    public void SerializePatterns_WithNullInput_ReturnsEmptyJsonArray()
    {
        Assert.That(FlowZoneGroupMatcher.SerializePatterns(null), Is.EqualTo("[]"));
    }

    [Test]
    public void IsZoneGroupName_WithMixedCaseSensitivity_MatchesConfiguredPatternsOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz_zone", kSuffixPatterns), Is.True);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz_ZONE", kSuffixPatterns), Is.True);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz-zone", kSuffixPatterns), Is.True);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz-ZONE", kSuffixPatterns), Is.False);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("zone_dmz", kSuffixPatterns), Is.False);
        });
    }

    [Test]
    public void IsZoneGroupName_WithoutPatternsOrName_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz_zone", null), Is.False);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("dmz_zone", kNoPatterns), Is.False);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName(null, kSuffixPatterns), Is.False);
            Assert.That(FlowZoneGroupMatcher.IsZoneGroupName("  ", kSuffixPatterns), Is.False);
        });
    }

    [Test]
    public void Matches_AppliesEveryMatchType()
    {
        FlowZoneGroupPattern prefixPattern = new() { MatchType = FlowZoneNameMatchType.Prefix, Value = "zone-" };
        FlowZoneGroupPattern containsPattern = new() { MatchType = FlowZoneNameMatchType.Contains, Value = "zone" };
        FlowZoneGroupPattern exactPattern = new() { MatchType = FlowZoneNameMatchType.Exact, Value = "zone1" };
        FlowZoneGroupPattern caseSensitiveExactPattern =
            new() { MatchType = FlowZoneNameMatchType.Exact, CaseSensitive = true, Value = "zone1" };

        Assert.Multiple(() =>
        {
            Assert.That(FlowZoneGroupMatcher.Matches("zone-dmz", prefixPattern), Is.True);
            Assert.That(FlowZoneGroupMatcher.Matches("dmz-zone", prefixPattern), Is.False);
            Assert.That(FlowZoneGroupMatcher.Matches("my-zone-dmz", containsPattern), Is.True);
            Assert.That(FlowZoneGroupMatcher.Matches("my-dmz", containsPattern), Is.False);
            Assert.That(FlowZoneGroupMatcher.Matches("ZONE1", exactPattern), Is.True);
            Assert.That(FlowZoneGroupMatcher.Matches("ZONE1", caseSensitiveExactPattern), Is.False);
            Assert.That(FlowZoneGroupMatcher.Matches("zone1", caseSensitiveExactPattern), Is.True);
        });
    }

    [Test]
    public void Matches_WithMissingNameOrPattern_ReturnsFalse()
    {
        FlowZoneGroupPattern emptyPattern = new() { MatchType = FlowZoneNameMatchType.Suffix, Value = "" };

        Assert.Multiple(() =>
        {
            Assert.That(FlowZoneGroupMatcher.Matches(null, kSuffixPatterns[0]), Is.False);
            Assert.That(FlowZoneGroupMatcher.Matches("dmz_zone", null), Is.False);
            Assert.That(FlowZoneGroupMatcher.Matches("dmz_zone", emptyPattern), Is.False);
        });
    }
}
