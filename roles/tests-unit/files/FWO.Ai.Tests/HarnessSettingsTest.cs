using FWO.Ai.Harness.Hosting;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests reading the settings of the harness server from the configuration: the defaults, the configured values,
/// and the checks of the server settings.
/// </summary>
[TestFixture]
internal class HarnessSettingsTest
{
    private const string kTechnicalUser = "ai-harness-test";
    private const string kPasswordFile = "/tmp/ai_harness_test_pwd";
    private const string kLoopbackMcpEndpoint = "http://127.0.0.1:9999/mcp";
    private const string kRemoteHttpMcpEndpoint = "http://middleware.example.com/mcp";
    private const string kConnectionTimeout = "00:00:30";
    private const int kRunsPerMinute = 5;
    private const long kMaxRequestBodyBytes = 1024L * 1024;
    private const int kMaxConcurrentRunsPerUser = 2;
    private const int kSettingsRefreshSeconds = 5;

    private static readonly List<TestCaseData> kInvalidSettings =
    [
        new TestCaseData("McpEndpoint", kRemoteHttpMcpEndpoint).SetName("RemoteHttpMcpEndpoint"),
        new TestCaseData("McpConnectionTimeout", "00:00:00").SetName("NoConnectionTimeout"),
        new TestCaseData("RunsPerMinute", "0").SetName("NoRuns"),
        new TestCaseData("MaxConcurrentRunsPerUser", "0").SetName("NoConcurrentRuns"),
        new TestCaseData("MaxRequestBodyBytes", "0").SetName("NoRequestBody"),
        new TestCaseData("MaxRequestBodyBytes", (HarnessSettings.kMaxRequestBodyBytesLimit + 1).ToString()).SetName("RequestBodyBeyondLimit"),
        new TestCaseData("TechnicalUser", " ").SetName("NoTechnicalUser"),
        new TestCaseData("TechnicalUserPasswordFile", " ").SetName("NoPasswordFile"),
        new TestCaseData("SettingsRefreshSeconds", "0").SetName("NoRefreshInterval"),
        new TestCaseData("SettingsRefreshSeconds", (HarnessSettings.kMaxSettingsRefreshSeconds + 1).ToString()).SetName("RefreshIntervalBeyondLimit")
    ];

    /// <summary>
    /// Verifies that without configuration the defaults apply: the MCP server on the same host, the default limits,
    /// and the technical user and its password file as the installer sets them up.
    /// </summary>
    [Test]
    public void FromConfiguration_WithoutSection_UsesDefaults()
    {
        Dictionary<string, string?> settingsValues = [];
        HarnessSettings settings = HarnessSettings.FromConfiguration(CreateConfiguration(settingsValues));

        Assert.That(settings.McpEndpoint, Is.EqualTo(new Uri(HarnessSettings.kDefaultMcpEndpoint)));
        Assert.That(settings.McpConnectionTimeout, Is.Null);
        Assert.That(settings.RunsPerMinute, Is.EqualTo(HarnessSettings.kDefaultRunsPerMinute));
        Assert.That(settings.MaxConcurrentRunsPerUser, Is.EqualTo(HarnessSettings.kDefaultMaxConcurrentRunsPerUser));
        Assert.That(settings.MaxRequestBodyBytes, Is.EqualTo(HarnessSettings.kDefaultMaxRequestBodyBytes));
        Assert.That(settings.TechnicalUser, Is.EqualTo(HarnessSettings.kDefaultTechnicalUser));
        Assert.That(settings.TechnicalUserPasswordFile, Is.EqualTo(HarnessSettings.kDefaultTechnicalUserPasswordFile));
        Assert.That(settings.SettingsRefreshSeconds, Is.EqualTo(HarnessSettings.kDefaultSettingsRefreshSeconds));
    }

    /// <summary>
    /// Verifies that the configured values are read, e.g. from the environment variables of the systemd unit.
    /// </summary>
    [Test]
    public void FromConfiguration_WithSection_ReadsValues()
    {
        Dictionary<string, string?> values = new()
        {
            ["Harness:McpEndpoint"] = kLoopbackMcpEndpoint,
            ["Harness:McpConnectionTimeout"] = kConnectionTimeout,
            ["Harness:RunsPerMinute"] = kRunsPerMinute.ToString(),
            ["Harness:MaxConcurrentRunsPerUser"] = kMaxConcurrentRunsPerUser.ToString(),
            ["Harness:MaxRequestBodyBytes"] = kMaxRequestBodyBytes.ToString(),
            ["Harness:TechnicalUser"] = kTechnicalUser,
            ["Harness:TechnicalUserPasswordFile"] = kPasswordFile,
            ["Harness:SettingsRefreshSeconds"] = kSettingsRefreshSeconds.ToString()
        };

        HarnessSettings settings = HarnessSettings.FromConfiguration(CreateConfiguration(values));

        Assert.That(settings.McpEndpoint, Is.EqualTo(new Uri(kLoopbackMcpEndpoint)));
        Assert.That(settings.McpConnectionTimeout, Is.EqualTo(TimeSpan.Parse(kConnectionTimeout)));
        Assert.That(settings.RunsPerMinute, Is.EqualTo(kRunsPerMinute));
        Assert.That(settings.MaxConcurrentRunsPerUser, Is.EqualTo(kMaxConcurrentRunsPerUser));
        Assert.That(settings.MaxRequestBodyBytes, Is.EqualTo(kMaxRequestBodyBytes));
        Assert.That(settings.TechnicalUser, Is.EqualTo(kTechnicalUser));
        Assert.That(settings.TechnicalUserPasswordFile, Is.EqualTo(kPasswordFile));
        Assert.That(settings.SettingsRefreshSeconds, Is.EqualTo(kSettingsRefreshSeconds));
    }

    /// <summary>
    /// Verifies that invalid server settings are refused when they are read.
    /// </summary>
    [TestCaseSource(nameof(kInvalidSettings))]
    public void FromConfiguration_WithInvalidSetting_Throws(string key, string value)
    {
        Dictionary<string, string?> values = new() { [$"{HarnessSettings.kSectionName}:{key}"] = value };

        Assert.Catch<ArgumentException>(() => HarnessSettings.FromConfiguration(CreateConfiguration(values)));
    }

    /// <summary>
    /// Verifies that a missing configuration is refused.
    /// </summary>
    [Test]
    public void FromConfiguration_WithoutConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HarnessSettings.FromConfiguration(null!));
    }

    /// <summary>
    /// Creates a configuration with the given values.
    /// </summary>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
