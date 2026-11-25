using FWO.Ai.Harness.Agent;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the defaults of the agent settings, the compaction reserve derived from them and their snapshot.
/// </summary>
[TestFixture]
internal class HarnessAgentSettingsTest
{
    private const int kDocumentedDefaultIterations = 10;
    private const int kDocumentedDefaultErrors = 3;
    private const int kContextWindowTokens = 1000;
    private const int kQuarterOfContextWindow = 250;
    private const int kExplicitReserveTokens = 600;
    private const int kIterations = 5;
    private const int kErrors = 1;
    private const int kChangedValue = 99;

    /// <summary>
    /// Verifies the defaults: 10 iterations, 3 consecutive errors and no explicit reserve.
    /// </summary>
    [Test]
    public void New_HasDefaults()
    {
        AgentSettings settings = new();

        Assert.That(settings.MaxIterationsPerRequest, Is.EqualTo(AgentSettings.kDefaultMaxIterationsPerRequest));
        Assert.That(AgentSettings.kDefaultMaxIterationsPerRequest, Is.EqualTo(kDocumentedDefaultIterations));
        Assert.That(settings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(AgentSettings.kDefaultMaxConsecutiveErrorsPerRequest));
        Assert.That(AgentSettings.kDefaultMaxConsecutiveErrorsPerRequest, Is.EqualTo(kDocumentedDefaultErrors));
        Assert.That(settings.CompactionReserveTokens, Is.Null);
    }

    /// <summary>
    /// Verifies that the compaction reserve is a quarter of the context window unless set explicitly.
    /// </summary>
    [Test]
    public void GetCompactionReserveTokens_IsQuarterOfContextWindowUnlessSet()
    {
        AgentSettings defaultSettings = new();
        AgentSettings explicitSettings = new() { CompactionReserveTokens = kExplicitReserveTokens };

        Assert.That(defaultSettings.GetCompactionReserveTokens(kContextWindowTokens), Is.EqualTo(kQuarterOfContextWindow));
        Assert.That(explicitSettings.GetCompactionReserveTokens(kContextWindowTokens), Is.EqualTo(kExplicitReserveTokens));
    }

    /// <summary>
    /// Verifies that a snapshot copies every setting and is not changed by later changes of the original.
    /// </summary>
    [Test]
    public void CreateSnapshot_CopiesSettingsIndependently()
    {
        AgentSettings settings = new()
        {
            MaxIterationsPerRequest = kIterations,
            MaxConsecutiveErrorsPerRequest = kErrors,
            CompactionReserveTokens = kExplicitReserveTokens
        };

        AgentSettings snapshot = settings.CreateSnapshot();
        settings.MaxIterationsPerRequest = kChangedValue;
        settings.MaxConsecutiveErrorsPerRequest = kChangedValue;
        settings.CompactionReserveTokens = kChangedValue;

        Assert.That(snapshot, Is.Not.SameAs(settings));
        Assert.That(snapshot.MaxIterationsPerRequest, Is.EqualTo(kIterations));
        Assert.That(snapshot.MaxConsecutiveErrorsPerRequest, Is.EqualTo(kErrors));
        Assert.That(snapshot.CompactionReserveTokens, Is.EqualTo(kExplicitReserveTokens));
    }
}
