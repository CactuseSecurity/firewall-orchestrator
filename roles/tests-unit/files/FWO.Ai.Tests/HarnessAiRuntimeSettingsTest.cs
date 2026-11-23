using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Providers;
using FWO.Ai.Harness.Settings;
using FWO.Basics;
using FWO.Data.Ai;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the runtime AI settings: how they map to the settings of the provider and the agent, and when two of them are
/// the same, so that the agent factory is kept.
/// </summary>
[TestFixture]
internal class HarnessAiRuntimeSettingsTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-runtime-test-key-4711";
    private const string kOtherApiKey = "sk-runtime-test-key-0815";
    private const string kEndpoint = "https://llm.example.com/v1";
    private const string kTokenUrl = "https://sso.example.com/realms/fwo/protocol/openid-connect/token";
    private const string kClientId = "fwo-ai-client";
    private const int kContextWindowTokens = 100000;
    private const int kMaxOutputTokens = 4000;
    private const int kMaxIterations = 6;
    private const int kMaxConsecutiveErrors = 2;
    private const int kCompactionReserveTokens = 30000;
    private const int kCredentialId = 7;

    /// <summary>
    /// Verifies that all values are passed on to the provider and agent settings.
    /// </summary>
    [Test]
    public void ToProviderAndAgentSettings_PassValuesOn()
    {
        AiRuntimeSettings runtimeSettings = new(CreateSettings(AiProvider.OpenAi), kApiKey);

        AiProviderSettings providerSettings = runtimeSettings.ToProviderSettings();
        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();

        Assert.That(providerSettings.Provider, Is.EqualTo(AiProvider.OpenAi));
        Assert.That(providerSettings.Model, Is.EqualTo(kModel));
        Assert.That(providerSettings.ApiKey, Is.EqualTo(kApiKey));
        Assert.That(providerSettings.Endpoint, Is.EqualTo(new Uri(kEndpoint)));
        Assert.That(providerSettings.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
        Assert.That(providerSettings.MaxOutputTokens, Is.EqualTo(kMaxOutputTokens));
        Assert.That(agentSettings.MaxIterationsPerRequest, Is.EqualTo(kMaxIterations));
        Assert.That(agentSettings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(kMaxConsecutiveErrors));
        Assert.That(agentSettings.CompactionReserveTokens, Is.EqualTo(kCompactionReserveTokens));
    }

    /// <summary>
    /// Verifies that 0 and empty values stand for the defaults, and that Ollama gets no API key.
    /// </summary>
    [Test]
    public void ToProviderAndAgentSettings_WithDefaults_LeaveThemUnset()
    {
        AiSettings settings = CreateSettings(AiProvider.Ollama);
        settings.Endpoint = "";
        settings.MaxOutputTokens = 0;
        settings.CompactionReserveTokens = 0;
        AiRuntimeSettings runtimeSettings = new(settings, kApiKey);

        AiProviderSettings providerSettings = runtimeSettings.ToProviderSettings();
        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();

        Assert.That(providerSettings.ApiKey, Is.Null);
        Assert.That(providerSettings.Endpoint, Is.Null);
        Assert.That(providerSettings.MaxOutputTokens, Is.Null);
        Assert.That(agentSettings.CompactionReserveTokens, Is.Null);
    }

    /// <summary>
    /// Verifies that an endpoint that is not an absolute URI is refused without naming it.
    /// </summary>
    [Test]
    public void ToProviderSettings_WithRelativeEndpoint_Throws()
    {
        AiSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.Endpoint = "llm/v1";

        ArgumentException? exception = Assert.Throws<ArgumentException>(() => new AiRuntimeSettings(settings, kApiKey).ToProviderSettings());

        Assert.That(exception!.Message, Does.Not.Contain(settings.Endpoint));
    }

    /// <summary>
    /// Verifies that settings with the same values are the same, and that a changed API key, value or problem is not.
    /// </summary>
    [Test]
    public void HasSameValues_ComparesAllValues()
    {
        AiRuntimeSettings settings = new(CreateSettings(AiProvider.OpenAi), kApiKey);
        AiSettings changedModel = CreateSettings(AiProvider.OpenAi);
        changedModel.Model = "other-model";

        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey)), Is.True);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kOtherApiKey)), Is.False);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(changedModel, kApiKey)), Is.False);
        Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey, "problem")), Is.False);
        Assert.That(settings.HasSameValues(null), Is.False);
    }

    /// <summary>
    /// Verifies that the switches of the AI functions are passed on to the agent settings, and that switching one
    /// changes the settings, so that the refresher replaces the agent factory.
    /// </summary>
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ToAgentSettings_PassesSwitchesOnAndHasSameValuesComparesThem(bool chatEnabled, bool reasonCheckEnabled)
    {
        AiSettings switchedSettings = CreateSettings(AiProvider.OpenAi);
        switchedSettings.ChatEnabled = chatEnabled;
        switchedSettings.ReasonCheckEnabled = reasonCheckEnabled;
        AiRuntimeSettings runtimeSettings = new(switchedSettings, kApiKey);

        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();

        Assert.Multiple(() =>
        {
            Assert.That(agentSettings.ChatEnabled, Is.EqualTo(chatEnabled));
            Assert.That(agentSettings.ReasonCheckEnabled, Is.EqualTo(reasonCheckEnabled));
            Assert.That(runtimeSettings.HasSameValues(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey)), Is.False);
            Assert.That(runtimeSettings.HasSameValues(new AiRuntimeSettings(switchedSettings, kApiKey)), Is.True);
        });
    }

    /// <summary>
    /// Verifies that the roles of the AI functions are passed on to the agent settings as copies, and that a change of
    /// the roles changes the settings, so that the refresher replaces the agent factory, while the same roles in
    /// another list or order do not.
    /// </summary>
    [Test]
    public void ToAgentSettings_PassesRolesOnAndHasSameValuesComparesThem()
    {
        AiSettings limitedSettings = CreateSettings(AiProvider.OpenAi);
        limitedSettings.ChatRoles = [Roles.Admin, Roles.Modeller];
        limitedSettings.ReasonCheckRoles = [Roles.Modeller];
        AiSettings reorderedSettings = CreateSettings(AiProvider.OpenAi);
        reorderedSettings.ChatRoles = [Roles.Modeller, Roles.Admin];
        reorderedSettings.ReasonCheckRoles = [Roles.Modeller];
        AiSettings otherReasonCheckRoles = CreateSettings(AiProvider.OpenAi);
        otherReasonCheckRoles.ChatRoles = [Roles.Admin, Roles.Modeller];
        AiRuntimeSettings runtimeSettings = new(limitedSettings, kApiKey);

        AgentSettings agentSettings = runtimeSettings.ToAgentSettings();
        limitedSettings.ChatRoles.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(agentSettings.ChatRoles, Is.EquivalentTo(reorderedSettings.ChatRoles));
            Assert.That(agentSettings.ReasonCheckRoles, Is.EqualTo(reorderedSettings.ReasonCheckRoles));
            Assert.That(new AiRuntimeSettings(reorderedSettings, kApiKey).HasSameValues(new AiRuntimeSettings(reorderedSettings, kApiKey)), Is.True);
            Assert.That(new AiRuntimeSettings(otherReasonCheckRoles, kApiKey).HasSameValues(new AiRuntimeSettings(reorderedSettings, kApiKey)), Is.False);
            Assert.That(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey).HasSameValues(new AiRuntimeSettings(reorderedSettings, kApiKey)), Is.False);
        });
    }

    /// <summary>
    /// Verifies that OAuth 2.0 gives the provider the client credentials with the token URL instead of an API key.
    /// </summary>
    [Test]
    public void ToProviderSettings_WithOAuth_PassesClientCredentialsOn()
    {
        AiRuntimeSettings runtimeSettings = new(CreateOAuthSettings(), kApiKey, clientId: kClientId);

        AiProviderSettings providerSettings = runtimeSettings.ToProviderSettings();

        Assert.Multiple(() =>
        {
            Assert.That(providerSettings.ApiKey, Is.Null);
            Assert.That(providerSettings.OAuthClient, Is.Not.Null);
            Assert.That(providerSettings.OAuthClient!.TokenUrl, Is.EqualTo(new Uri(kTokenUrl)));
            Assert.That(providerSettings.OAuthClient.ClientId, Is.EqualTo(kClientId));
            Assert.That(providerSettings.OAuthClient.ClientSecret, Is.EqualTo(kApiKey));
            Assert.That(providerSettings.OAuthClient.Scope, Is.Null);
            Assert.That(new AiRuntimeSettings(CreateSettings(AiProvider.OpenAi), kApiKey).ToProviderSettings().OAuthClient, Is.Null);
        });
    }

    /// <summary>
    /// Verifies that OAuth 2.0 without token URL, with a relative one, or without client id or secret is refused,
    /// without naming the token URL or the secret.
    /// </summary>
    [TestCase("", kClientId, kApiKey)]
    [TestCase("sso/token", kClientId, kApiKey)]
    [TestCase(kTokenUrl, null, kApiKey)]
    [TestCase(kTokenUrl, kClientId, null)]
    public void ToProviderSettings_WithIncompleteOAuth_Throws(string tokenUrl, string? clientId, string? secret)
    {
        AiSettings settings = CreateOAuthSettings();
        settings.TokenUrl = tokenUrl;

        ArgumentException? exception = Assert.Throws<ArgumentException>(() => new AiRuntimeSettings(settings, secret, clientId: clientId).ToProviderSettings());

        Assert.That(exception!.Message, Does.Not.Contain(kApiKey));
        Assert.That(exception.Message, Does.Not.Contain("sso/token"));
    }

    /// <summary>
    /// Verifies that a changed authentication method, token URL or client id makes the settings differ.
    /// </summary>
    [Test]
    public void HasSameValues_ComparesOAuthValues()
    {
        AiRuntimeSettings settings = new(CreateOAuthSettings(), kApiKey, clientId: kClientId);
        AiSettings otherTokenUrl = CreateOAuthSettings();
        otherTokenUrl.TokenUrl = "https://sso.example.com/realms/other/protocol/openid-connect/token";
        AiSettings apiKeyMethod = CreateOAuthSettings();
        apiKeyMethod.AuthMethod = AiAuthMethod.ApiKey;

        Assert.Multiple(() =>
        {
            Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateOAuthSettings(), kApiKey, clientId: kClientId)), Is.True);
            Assert.That(settings.HasSameValues(new AiRuntimeSettings(CreateOAuthSettings(), kApiKey, clientId: "other-client")), Is.False);
            Assert.That(settings.HasSameValues(new AiRuntimeSettings(otherTokenUrl, kApiKey, clientId: kClientId)), Is.False);
            Assert.That(settings.HasSameValues(new AiRuntimeSettings(apiKeyMethod, kApiKey, clientId: kClientId)), Is.False);
        });
    }

    /// <summary>
    /// Verifies that the API key does not show in the text of the settings, e.g. in a log message.
    /// </summary>
    [Test]
    public void ToString_DoesNotContainApiKey()
    {
        AiRuntimeSettings settings = new(CreateSettings(AiProvider.OpenAi), kApiKey);

        Assert.That(settings.ToString(), Does.Not.Contain(kApiKey));
        Assert.That(settings.ToProviderSettings().ToString(), Does.Not.Contain(kApiKey));
    }

    /// <summary>
    /// Creates valid settings of the given provider.
    /// </summary>
    private static AiSettings CreateSettings(AiProvider provider)
    {
        return new AiSettings
        {
            Provider = provider,
            Model = kModel,
            Endpoint = kEndpoint,
            CredentialId = kCredentialId,
            ContextWindowTokens = kContextWindowTokens,
            MaxOutputTokens = kMaxOutputTokens,
            MaxIterationsPerRequest = kMaxIterations,
            MaxConsecutiveErrorsPerRequest = kMaxConsecutiveErrors,
            CompactionReserveTokens = kCompactionReserveTokens
        };
    }

    /// <summary>
    /// Creates valid settings of an OpenAI compatible server with OAuth 2.0.
    /// </summary>
    private static AiSettings CreateOAuthSettings()
    {
        AiSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.AuthMethod = AiAuthMethod.OAuth2ClientCredentials;
        settings.TokenUrl = kTokenUrl;
        return settings;
    }
}
