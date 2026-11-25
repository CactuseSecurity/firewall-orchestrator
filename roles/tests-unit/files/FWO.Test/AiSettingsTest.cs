using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data.Ai;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the AI settings of the AI settings page: reading them from the global config items the way the AI harness does,
/// checking them, and storing them in the global config.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AiSettingsTest
{
    private const string kModel = "gpt-test";
    private const string kHttpsEndpoint = "https://llm.example.com/v1";
    private const string kTokenUrl = "https://sso.example.com/realms/fwo/protocol/openid-connect/token";
    private const int kCredentialId = 4;
    private const int kContextWindowTokens = 100000;
    private static readonly List<AiSettingsProblem> kCredentialMissing = [AiSettingsProblem.CredentialMissing];
    private static readonly List<string> kNoRoles = [];
    private static readonly List<string> kAdminRoles = [Roles.Admin];
    private static readonly List<string> kModellerRoles = [Roles.Modeller];
    private static readonly List<string> kReporterRoles = [Roles.Reporter];
    private static readonly List<string> kAdminAndModellerRoles = [Roles.Admin, Roles.Modeller];
    private const string kStoredCustomHeaders = """[{"name":"x-gateway-profile","value":"my-profile"},{"name":"token-issuer","value":"keycloak"}]""";
    private static readonly List<string> kCustomHeaderNames = ["x-gateway-profile", "token-issuer"];
    private static readonly List<string> kCustomHeaderValues = ["my-profile", "keycloak"];
    private static readonly List<AiSettingsProblem> kCustomHeadersUnreadable = [AiSettingsProblem.CustomHeadersUnreadable];
    private static readonly List<AiSettingsProblem> kCustomHeaderNameReserved = [AiSettingsProblem.CustomHeaderNameReserved];
    private static readonly List<AiSettingsProblem> kRequestTimeoutInvalid = [AiSettingsProblem.RequestTimeoutInvalid];

    /// <summary>
    /// Verifies that all items are read, the provider ignoring case and the texts trimmed.
    /// </summary>
    [Test]
    public void FromConfigValues_ReadsAllItems()
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kProviderKey] = "anthropic",
            [AiSettings.kModelKey] = $" {kModel} ",
            [AiSettings.kEndpointKey] = $" {kHttpsEndpoint} ",
            [AiSettings.kCredentialIdKey] = "4",
            [AiSettings.kContextWindowTokensKey] = "100000",
            [AiSettings.kMaxOutputTokensKey] = "4000",
            [AiSettings.kRequestTimeoutSecondsKey] = "900",
            [AiSettings.kMaxIterationsPerRequestKey] = "7",
            [AiSettings.kMaxConsecutiveErrorsPerRequestKey] = "2",
            [AiSettings.kCompactionReserveTokensKey] = "30000"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Provider, Is.EqualTo(AiProvider.Anthropic));
            Assert.That(settings.Model, Is.EqualTo(kModel));
            Assert.That(settings.Endpoint, Is.EqualTo(kHttpsEndpoint));
            Assert.That(settings.CredentialId, Is.EqualTo(kCredentialId));
            Assert.That(settings.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
            Assert.That(settings.MaxOutputTokens, Is.EqualTo(4000));
            Assert.That(settings.RequestTimeoutSeconds, Is.EqualTo(900));
            Assert.That(settings.MaxIterationsPerRequest, Is.EqualTo(7));
            Assert.That(settings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(2));
            Assert.That(settings.CompactionReserveTokens, Is.EqualTo(30000));
            Assert.That(settings.Validate(), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that missing, empty, unknown and numeric values fall back to the defaults.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("ChatBot")]
    [TestCase("2")]
    [TestCase("99")]
    public void FromConfigValues_WithUnreadableValues_UsesDefaults(string? value)
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kProviderKey] = value,
            [AiSettings.kMaxIterationsPerRequestKey] = value,
            [AiSettings.kMaxConsecutiveErrorsPerRequestKey] = "three",
            [AiSettings.kRequestTimeoutSecondsKey] = "ten minutes"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Provider, Is.EqualTo(AiProvider.None));
            Assert.That(settings.RequestTimeoutSeconds, Is.EqualTo(AiSettings.kDefaultRequestTimeoutSeconds));
            Assert.That(settings.IsConfigured, Is.False);
            Assert.That(settings.MaxIterationsPerRequest, Is.EqualTo(value is "2" or "99" ? int.Parse(value) : AiSettings.kDefaultMaxIterationsPerRequest));
            Assert.That(settings.MaxConsecutiveErrorsPerRequest, Is.EqualTo(AiSettings.kDefaultMaxConsecutiveErrorsPerRequest));
            Assert.That(settings.Model, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that settings without provider are not checked, as they are not used.
    /// </summary>
    [Test]
    public void Validate_WithoutProvider_HasNoProblems()
    {
        AiSettings settings = new() { ContextWindowTokens = -1, MaxIterationsPerRequest = 0 };

        Assert.That(settings.Validate(), Is.Empty);
    }

    /// <summary>
    /// Verifies that a provider other than Ollama needs a credential, and Ollama does not.
    /// </summary>
    [Test]
    public void Validate_RequiresCredentialForApiKeyProvidersOnly()
    {
        AiSettings openAi = CreateValidSettings(AiProvider.OpenAi);
        openAi.CredentialId = 0;
        AiSettings ollama = CreateValidSettings(AiProvider.Ollama);
        ollama.CredentialId = 0;

        Assert.Multiple(() =>
        {
            Assert.That(openAi.Validate(), Is.EqualTo(kCredentialMissing));
            Assert.That(openAi.RequiresApiKey, Is.True);
            Assert.That(ollama.Validate(), Is.Empty);
            Assert.That(ollama.RequiresApiKey, Is.False);
        });
    }

    /// <summary>
    /// Verifies the checks of the endpoint: an absolute http or https URL, and https if the API key is sent to a host
    /// other than the local one.
    /// </summary>
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, null)]
    [TestCase(AiProvider.OpenAi, "http://127.0.0.1:8000/v1", null)]
    [TestCase(AiProvider.OpenAi, "http://localhost:8000/v1", null)]
    [TestCase(AiProvider.OpenAi, "http://llm.example.com/v1", AiSettingsProblem.EndpointNotSecure)]
    [TestCase(AiProvider.Ollama, "http://gpu-host.example.com:11434", null)]
    [TestCase(AiProvider.OpenAi, "llm.example.com/v1", AiSettingsProblem.EndpointInvalid)]
    [TestCase(AiProvider.OpenAi, "ftp://llm.example.com/v1", AiSettingsProblem.EndpointInvalid)]
    [TestCase(AiProvider.OpenAi, "javascript:alert(1)", AiSettingsProblem.EndpointInvalid)]
    public void Validate_ChecksEndpoint(AiProvider provider, string endpoint, AiSettingsProblem? expectedProblem)
    {
        AiSettings settings = CreateValidSettings(provider);
        settings.Endpoint = endpoint;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(problems, expectedProblem == null ? Is.Empty : Has.Count.EqualTo(1).And.Contains(expectedProblem.Value));
    }

    /// <summary>
    /// Verifies that the authentication method and the token URL are read, the method ignoring case and an unknown or
    /// numeric method falling back to the API key.
    /// </summary>
    [TestCase("oauth2clientcredentials", AiAuthMethod.OAuth2ClientCredentials)]
    [TestCase("ApiKey", AiAuthMethod.ApiKey)]
    [TestCase("1", AiAuthMethod.ApiKey)]
    [TestCase("Kerberos", AiAuthMethod.ApiKey)]
    [TestCase(null, AiAuthMethod.ApiKey)]
    public void FromConfigValues_ReadsAuthMethodAndTokenUrl(string? authMethod, AiAuthMethod expectedMethod)
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kProviderKey] = "OpenAi",
            [AiSettings.kAuthMethodKey] = authMethod,
            [AiSettings.kTokenUrlKey] = $" {kTokenUrl} "
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.AuthMethod, Is.EqualTo(expectedMethod));
            Assert.That(settings.TokenUrl, Is.EqualTo(kTokenUrl));
        });
    }

    /// <summary>
    /// Verifies that the switches of the AI functions are read ignoring case, and are off for a missing or unreadable
    /// value, so that a function is only offered once it is switched on. Their keys are among those the harness reads.
    /// </summary>
    [TestCase("True", true)]
    [TestCase("true", true)]
    [TestCase(" TRUE ", true)]
    [TestCase("False", false)]
    [TestCase("1", false)]
    [TestCase("yes", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void FromConfigValues_ReadsSwitchesOffUnlessTrue(string? value, bool expected)
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kChatKey] = value,
            [AiSettings.kReasonCheckKey] = value
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.ChatEnabled, Is.EqualTo(expected));
            Assert.That(settings.ReasonCheckEnabled, Is.EqualTo(expected));
            Assert.That(AiSettings.kKeys, Does.Contain(AiSettings.kChatKey));
            Assert.That(AiSettings.kKeys, Does.Contain(AiSettings.kReasonCheckKey));
        });
    }

    /// <summary>
    /// Verifies that each switch is read from its own config key.
    /// </summary>
    [Test]
    public void FromConfigValues_ReadsEachSwitchFromItsKey()
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kChatKey] = "True",
            [AiSettings.kReasonCheckKey] = "False"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.That(settings.ChatEnabled, Is.True);
        Assert.That(settings.ReasonCheckEnabled, Is.False);
    }

    /// <summary>
    /// Verifies that the role filters are read from their own keys, normalized, which the harness reads too, and that
    /// they leave the switches as they are.
    /// </summary>
    [Test]
    public void FromConfigValues_ReadsRolesOfEachFunctionFromItsKey()
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kChatKey] = "True",
            [AiSettings.kReasonCheckKey] = "True",
            [AiSettings.kChatRolesKey] = "[\"modeller\",\" admin \"]",
            [AiSettings.kReasonCheckRolesKey] = "[\"modeller\"]"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.ChatEnabled, Is.True);
            Assert.That(settings.ReasonCheckEnabled, Is.True);
            Assert.That(settings.ChatRoles, Is.EqualTo(kAdminAndModellerRoles));
            Assert.That(settings.ReasonCheckRoles, Is.EqualTo(kModellerRoles));
            Assert.That(AiSettings.kKeys, Does.Contain(AiSettings.kChatRolesKey));
            Assert.That(AiSettings.kKeys, Does.Contain(AiSettings.kReasonCheckRolesKey));
        });
    }

    /// <summary>
    /// Verifies that missing role filters restrict nothing, so that installations without them keep offering the
    /// functions that are switched on to every user.
    /// </summary>
    [Test]
    public void FromConfigValues_WithoutRoles_RestrictsNothing()
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kChatKey] = "True",
            [AiSettings.kReasonCheckKey] = "True"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.ChatRoles, Is.Empty);
            Assert.That(settings.ReasonCheckRoles, Is.Empty);
            Assert.That(settings.MayUseChat(kReporterRoles), Is.True);
            Assert.That(settings.MayUseReasonCheck(kNoRoles), Is.True);
        });
    }

    /// <summary>
    /// Verifies that an unreadable role filter switches its function off, so that it is not offered to every user by
    /// mistake, and leaves the other function as it is.
    /// </summary>
    [TestCase("admin")]
    [TestCase("{}")]
    [TestCase("[1]")]
    public void FromConfigValues_WithUnreadableRoles_SwitchesFunctionOff(string unreadableRoles)
    {
        Dictionary<string, string?> configValues = new()
        {
            [AiSettings.kChatKey] = "True",
            [AiSettings.kReasonCheckKey] = "True",
            [AiSettings.kChatRolesKey] = unreadableRoles,
            [AiSettings.kReasonCheckRolesKey] = "[\"modeller\"]"
        };

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(settings.ChatEnabled, Is.False);
            Assert.That(settings.ChatRoles, Is.Empty);
            Assert.That(settings.ReasonCheckEnabled, Is.True);
            Assert.That(settings.ReasonCheckRoles, Is.EqualTo(kModellerRoles));
        });
    }

    /// <summary>
    /// Verifies that a user may use a function only while it is switched on and the user holds one of its roles, each
    /// function by its own switch and roles.
    /// </summary>
    [Test]
    public void MayUse_NeedsSwitchAndOneOfTheRoles()
    {
        AiSettings settings = new() { ChatEnabled = true, ChatRoles = [Roles.Admin], ReasonCheckRoles = [Roles.Modeller] };

        Assert.Multiple(() =>
        {
            Assert.That(settings.MayUseChat(kAdminRoles), Is.True);
            Assert.That(settings.MayUseChat(kReporterRoles), Is.False);
            Assert.That(settings.MayUseReasonCheck(kModellerRoles), Is.False);
        });
        settings.ReasonCheckEnabled = true;
        Assert.That(settings.MayUseReasonCheck(kModellerRoles), Is.True);
        Assert.That(settings.MayUseReasonCheck(kAdminRoles), Is.False);
    }

    /// <summary>
    /// Verifies that OAuth 2.0 needs a credential too, but no API key.
    /// </summary>
    [Test]
    public void OAuthSettings_NeedACredentialButNoApiKey()
    {
        AiSettings oauth = CreateValidOAuthSettings();
        AiSettings apiKey = CreateValidSettings(AiProvider.OpenAi);
        AiSettings ollamaWithOAuth = CreateValidSettings(AiProvider.Ollama);
        ollamaWithOAuth.AuthMethod = AiAuthMethod.OAuth2ClientCredentials;

        Assert.Multiple(() =>
        {
            Assert.That(oauth.Validate(), Is.Empty);
            Assert.That(oauth.UsesOAuth, Is.True);
            Assert.That(oauth.RequiresCredential, Is.True);
            Assert.That(oauth.RequiresApiKey, Is.False);
            Assert.That(apiKey.UsesOAuth, Is.False);
            Assert.That(apiKey.RequiresApiKey, Is.True);
            Assert.That(ollamaWithOAuth.RequiresCredential, Is.False);
            Assert.That(new AiSettings { AuthMethod = AiAuthMethod.OAuth2ClientCredentials }.UsesOAuth, Is.False);
        });
        oauth.CredentialId = 0;
        Assert.That(oauth.Validate(), Is.EqualTo(kCredentialMissing));
    }

    /// <summary>
    /// Verifies the checks of OAuth 2.0: only for OpenAI with an endpoint, and a token URL that is absolute and uses
    /// https unless it is a loopback address.
    /// </summary>
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, kTokenUrl, null)]
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, "http://localhost:8080/realms/fwo/protocol/openid-connect/token", null)]
    [TestCase(AiProvider.Anthropic, kHttpsEndpoint, kTokenUrl, AiSettingsProblem.OAuthNotSupported)]
    [TestCase(AiProvider.Ollama, kHttpsEndpoint, kTokenUrl, AiSettingsProblem.OAuthNotSupported)]
    [TestCase(AiProvider.OpenAi, "", kTokenUrl, AiSettingsProblem.OAuthEndpointMissing)]
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, "", AiSettingsProblem.TokenUrlInvalid)]
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, "sso.example.com/token", AiSettingsProblem.TokenUrlInvalid)]
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, "http://sso.example.com/token", AiSettingsProblem.TokenUrlInvalid)]
    [TestCase(AiProvider.OpenAi, kHttpsEndpoint, "ftp://sso.example.com/token", AiSettingsProblem.TokenUrlInvalid)]
    [TestCase(AiProvider.OpenAi, "http://llm.example.com/v1", kTokenUrl, AiSettingsProblem.EndpointNotSecure)]
    public void Validate_ChecksOAuth(AiProvider provider, string endpoint, string tokenUrl, AiSettingsProblem? expectedProblem)
    {
        AiSettings settings = CreateValidOAuthSettings();
        settings.Provider = provider;
        settings.Endpoint = endpoint;
        settings.TokenUrl = tokenUrl;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(problems, expectedProblem == null ? Is.Empty : Has.Count.EqualTo(1).And.Contains(expectedProblem.Value));
    }

    /// <summary>
    /// Verifies that the token URL is not checked without OAuth 2.0, as it is not used.
    /// </summary>
    [Test]
    public void Validate_WithApiKey_IgnoresTokenUrl()
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);
        settings.TokenUrl = "not a url";

        Assert.That(settings.Validate(), Is.Empty);
    }

    /// <summary>
    /// Verifies the checks of the token limits and the agent loop.
    /// </summary>
    [TestCase(0, 0, 10, 3, 0, AiSettingsProblem.ContextWindowInvalid)]
    [TestCase(100000, -1, 10, 3, 0, AiSettingsProblem.MaxOutputTokensInvalid)]
    [TestCase(100000, 100000, 10, 3, 0, AiSettingsProblem.MaxOutputTokensInvalid)]
    [TestCase(100000, 0, 0, 3, 0, AiSettingsProblem.MaxIterationsInvalid)]
    [TestCase(100000, 0, 10, -1, 0, AiSettingsProblem.MaxConsecutiveErrorsInvalid)]
    [TestCase(100000, 0, 10, 3, -1, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 0, 10, 3, 100000, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 30000, 10, 3, 20000, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 30000, 10, 3, 0, AiSettingsProblem.CompactionReserveInvalid)]
    [TestCase(100000, 20000, 10, 3, 0, null)]
    [TestCase(100000, 30000, 10, 0, 40000, null)]
    public void Validate_ChecksLimits(int contextWindow, int maxOutput, int maxIterations, int maxErrors, int reserve,
        AiSettingsProblem? expectedProblem)
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);
        settings.ContextWindowTokens = contextWindow;
        settings.MaxOutputTokens = maxOutput;
        settings.MaxIterationsPerRequest = maxIterations;
        settings.MaxConsecutiveErrorsPerRequest = maxErrors;
        settings.CompactionReserveTokens = reserve;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(problems, expectedProblem == null ? Is.Empty : Does.Contain(expectedProblem.Value));
    }

    /// <summary>
    /// Verifies that the request timeout must be at least 1 second and at most an hour, and is valid by default.
    /// </summary>
    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(AiSettings.kMinRequestTimeoutSeconds, true)]
    [TestCase(AiSettings.kDefaultRequestTimeoutSeconds, true)]
    [TestCase(AiSettings.kMaxRequestTimeoutSeconds, true)]
    [TestCase(AiSettings.kMaxRequestTimeoutSeconds + 1, false)]
    public void Validate_ChecksRequestTimeout(int requestTimeoutSeconds, bool isValid)
    {
        AiSettings settings = CreateValidSettings(AiProvider.Ollama);
        int defaultRequestTimeoutSeconds = settings.RequestTimeoutSeconds;
        settings.RequestTimeoutSeconds = requestTimeoutSeconds;

        List<AiSettingsProblem> problems = settings.Validate();

        Assert.That(defaultRequestTimeoutSeconds, Is.EqualTo(AiSettings.kDefaultRequestTimeoutSeconds));
        Assert.That(problems, isValid ? Is.Empty : Is.EqualTo(kRequestTimeoutInvalid));
    }

    /// <summary>
    /// Verifies that the compaction reserve is a quarter of the context window unless it is set.
    /// </summary>
    [Test]
    public void GetEffectiveCompactionReserveTokens_DefaultsToQuarterOfContextWindow()
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);

        int defaultReserve = settings.GetEffectiveCompactionReserveTokens();
        settings.CompactionReserveTokens = 12345;

        Assert.That(defaultReserve, Is.EqualTo(kContextWindowTokens / AiSettings.kDefaultCompactionReserveDivisor));
        Assert.That(settings.GetEffectiveCompactionReserveTokens(), Is.EqualTo(12345));
    }

    /// <summary>
    /// Verifies that the AI settings are stored in the global config, with trimmed texts, and read back unchanged.
    /// </summary>
    [Test]
    public void ConfigExtensions_StoreAndReadSettings()
    {
        ConfigData config = new();
        AiSettings settings = CreateValidSettings(AiProvider.Google);
        settings.Model = $"  {kModel}\t";
        settings.Endpoint = $" {kHttpsEndpoint} ";
        settings.MaxOutputTokens = 2000;
        settings.RequestTimeoutSeconds = 1200;
        settings.MaxIterationsPerRequest = 5;
        settings.MaxConsecutiveErrorsPerRequest = 1;
        settings.CompactionReserveTokens = 25000;
        settings.AuthMethod = AiAuthMethod.OAuth2ClientCredentials;
        settings.TokenUrl = $" {kTokenUrl} ";
        settings.ChatEnabled = true;
        settings.ReasonCheckEnabled = true;

        config.SetAiSettings(settings);
        AiSettings readBack = config.GetAiSettings();

        Assert.Multiple(() =>
        {
            Assert.That(config.AiChatEnabled, Is.True);
            Assert.That(config.AiReasonCheckEnabled, Is.True);
            Assert.That(readBack.ChatEnabled, Is.True);
            Assert.That(readBack.ReasonCheckEnabled, Is.True);
            Assert.That(config.AiModel, Is.EqualTo(kModel));
            Assert.That(config.AiEndpoint, Is.EqualTo(kHttpsEndpoint));
            Assert.That(config.AiTokenUrl, Is.EqualTo(kTokenUrl));
            Assert.That(readBack.AuthMethod, Is.EqualTo(AiAuthMethod.OAuth2ClientCredentials));
            Assert.That(readBack.TokenUrl, Is.EqualTo(kTokenUrl));
            Assert.That(readBack.Provider, Is.EqualTo(AiProvider.Google));
            Assert.That(readBack.Model, Is.EqualTo(kModel));
            Assert.That(readBack.CredentialId, Is.EqualTo(kCredentialId));
            Assert.That(readBack.ContextWindowTokens, Is.EqualTo(kContextWindowTokens));
            Assert.That(readBack.MaxOutputTokens, Is.EqualTo(2000));
            Assert.That(config.AiRequestTimeoutSeconds, Is.EqualTo(1200));
            Assert.That(readBack.RequestTimeoutSeconds, Is.EqualTo(1200));
            Assert.That(readBack.MaxIterationsPerRequest, Is.EqualTo(5));
            Assert.That(readBack.MaxConsecutiveErrorsPerRequest, Is.EqualTo(1));
            Assert.That(readBack.CompactionReserveTokens, Is.EqualTo(25000));
            Assert.That(readBack, Is.Not.SameAs(settings));
        });
    }

    /// <summary>
    /// Verifies that the role filters are stored in the global config as normalized JSON arrays and read back.
    /// </summary>
    [Test]
    public void ConfigExtensions_StoreAndReadRoles()
    {
        ConfigData config = new();
        AiSettings settings = CreateValidSettings(AiProvider.Ollama);
        settings.ChatEnabled = true;
        settings.ReasonCheckEnabled = true;
        settings.ChatRoles = [Roles.Modeller, $" {Roles.Admin} ", Roles.Admin.ToUpperInvariant()];
        settings.ReasonCheckRoles = [Roles.Modeller];

        config.SetAiSettings(settings);
        AiSettings readBack = config.GetAiSettings();

        Assert.Multiple(() =>
        {
            Assert.That(config.AiChatRoles, Is.EqualTo("[\"admin\",\"modeller\"]"));
            Assert.That(config.AiReasonCheckRoles, Is.EqualTo("[\"modeller\"]"));
            Assert.That(readBack.ChatEnabled, Is.True);
            Assert.That(readBack.ReasonCheckEnabled, Is.True);
            Assert.That(readBack.ChatRoles, Is.EqualTo(kAdminAndModellerRoles));
            Assert.That(readBack.ReasonCheckRoles, Is.EqualTo(kModellerRoles));
        });
    }

    /// <summary>
    /// Verifies that the settings page reads a function with an unreadable role filter as switched off, the way the
    /// harness treats it, and that no user may use it.
    /// </summary>
    [Test]
    public void ConfigExtensions_WithUnreadableRoles_ReadFunctionSwitchedOff()
    {
        ConfigData config = new()
        {
            AiChatEnabled = true,
            AiReasonCheckEnabled = true,
            AiChatRoles = "not json",
            AiReasonCheckRoles = "{\"role\":\"modeller\"}"
        };

        AiSettings settings = config.GetAiSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ChatEnabled, Is.False);
            Assert.That(settings.ReasonCheckEnabled, Is.False);
            Assert.That(settings.ChatRoles, Is.Empty);
            Assert.That(settings.ReasonCheckRoles, Is.Empty);
            Assert.That(config.MayUseAiChat(kAdminRoles), Is.False);
            Assert.That(config.MayUseAiReasonCheck(kModellerRoles), Is.False);
        });
    }

    /// <summary>
    /// Verifies that the UI offers a function to a user only while it is switched on and the user holds one of its
    /// roles, or no roles are set.
    /// </summary>
    [Test]
    public void ConfigExtensions_MayUse_NeedsSwitchAndOneOfTheRoles()
    {
        ConfigData switchedOff = new() { AiChatRoles = "[\"admin\"]" };
        ConfigData limited = new()
        {
            AiChatEnabled = true,
            AiReasonCheckEnabled = true,
            AiChatRoles = "[\"admin\"]",
            AiReasonCheckRoles = "[\"modeller\"]"
        };
        ConfigData unlimited = new() { AiChatEnabled = true, AiReasonCheckEnabled = true };

        Assert.Multiple(() =>
        {
            Assert.That(switchedOff.MayUseAiChat(kAdminRoles), Is.False);
            Assert.That(limited.MayUseAiChat(kAdminAndModellerRoles), Is.True);
            Assert.That(limited.MayUseAiChat(kModellerRoles), Is.False);
            Assert.That(limited.MayUseAiReasonCheck(kModellerRoles), Is.True);
            Assert.That(limited.MayUseAiReasonCheck(kReporterRoles), Is.False);
            Assert.That(unlimited.MayUseAiChat(kNoRoles), Is.True);
            Assert.That(unlimited.MayUseAiReasonCheck(kReporterRoles), Is.True);
        });
    }

    /// <summary>
    /// Verifies that the defaults of the global config are those of the AI settings, so that an installation without
    /// stored AI settings behaves like the harness expects.
    /// </summary>
    [Test]
    public void ConfigData_DefaultsMatchAiSettings()
    {
        AiSettings fromConfig = new ConfigData().GetAiSettings();
        AiSettings defaults = new();

        Assert.Multiple(() =>
        {
            Assert.That(fromConfig.Provider, Is.EqualTo(defaults.Provider));
            Assert.That(fromConfig.AuthMethod, Is.EqualTo(defaults.AuthMethod));
            Assert.That(fromConfig.TokenUrl, Is.EqualTo(defaults.TokenUrl));
            Assert.That(fromConfig.MaxIterationsPerRequest, Is.EqualTo(defaults.MaxIterationsPerRequest));
            Assert.That(fromConfig.MaxConsecutiveErrorsPerRequest, Is.EqualTo(defaults.MaxConsecutiveErrorsPerRequest));
            Assert.That(fromConfig.CompactionReserveTokens, Is.EqualTo(defaults.CompactionReserveTokens));
            Assert.That(fromConfig.ChatEnabled, Is.False);
            Assert.That(fromConfig.ReasonCheckEnabled, Is.False);
            Assert.That(defaults.ChatEnabled, Is.False);
            Assert.That(defaults.ReasonCheckEnabled, Is.False);
            Assert.That(new ConfigData().AiChatRoles, Is.EqualTo(AiFunctionRoles.kNoRestriction));
            Assert.That(new ConfigData().AiReasonCheckRoles, Is.EqualTo(AiFunctionRoles.kNoRestriction));
            Assert.That(fromConfig.ChatRoles, Is.Empty);
            Assert.That(fromConfig.ReasonCheckRoles, Is.Empty);
            Assert.That(defaults.ChatRoles, Is.Empty);
            Assert.That(defaults.ReasonCheckRoles, Is.Empty);
            Assert.That(new ConfigData().AiCustomHeaders, Is.EqualTo(AiCustomHeaderList.kNone));
            Assert.That(fromConfig.CustomHeaders, Is.Empty);
            Assert.That(defaults.CustomHeaders, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that the additional headers are read from their key, normalized, and that settings with them are valid.
    /// </summary>
    [Test]
    public void FromConfigValues_ReadsCustomHeaders()
    {
        Dictionary<string, string?> configValues = CreateValidConfigValues();
        configValues[AiSettings.kCustomHeadersKey] = kStoredCustomHeaders;

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.Multiple(() =>
        {
            Assert.That(AiSettings.kKeys, Does.Contain(AiSettings.kCustomHeadersKey));
            Assert.That(settings.CustomHeaders.Select(header => header.Name), Is.EqualTo(kCustomHeaderNames));
            Assert.That(settings.CustomHeaders.Select(header => header.Value), Is.EqualTo(kCustomHeaderValues));
            Assert.That(settings.Validate(), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that stored additional headers the harness cannot read make the settings invalid, so that the harness does
    /// not talk to the provider without them.
    /// </summary>
    [TestCase("{not json")]
    [TestCase("null")]
    public void FromConfigValues_WithUnreadableCustomHeaders_IsInvalid(string storedHeaders)
    {
        Dictionary<string, string?> configValues = CreateValidConfigValues();
        configValues[AiSettings.kCustomHeadersKey] = storedHeaders;

        AiSettings settings = AiSettings.FromConfigValues(configValues);

        Assert.That(settings.CustomHeaders, Is.Empty);
        Assert.That(settings.Validate(), Is.EqualTo(kCustomHeadersUnreadable));
    }

    /// <summary>
    /// Verifies that the validation checks the additional headers as they are stored: rows left blank on the settings
    /// page are ignored, refused headers are reported.
    /// </summary>
    [Test]
    public void Validate_ChecksCustomHeaders()
    {
        AiSettings withBlankRow = CreateValidSettings(AiProvider.OpenAi);
        withBlankRow.CustomHeaders.Add(new AiCustomHeader { Name = " ", Value = " " });
        AiSettings withReservedHeader = CreateValidSettings(AiProvider.OpenAi);
        withReservedHeader.CustomHeaders.Add(new AiCustomHeader { Name = "Authorization", Value = "Bearer secret" });
        AiSettings withoutProvider = new();
        withoutProvider.CustomHeaders.Add(new AiCustomHeader { Name = "Authorization", Value = "Bearer secret" });

        Assert.Multiple(() =>
        {
            Assert.That(withBlankRow.Validate(), Is.Empty);
            Assert.That(withReservedHeader.Validate(), Is.EqualTo(kCustomHeaderNameReserved));
            Assert.That(withoutProvider.Validate(), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that the config stores the additional headers normalized and returns them as copies, which the harness
    /// reads the same way.
    /// </summary>
    [Test]
    public void ConfigExtensions_StoreAndReadCustomHeaders()
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);
        settings.CustomHeaders.Add(new AiCustomHeader { Name = " x-gateway-profile ", Value = " my-profile " });
        settings.CustomHeaders.Add(new AiCustomHeader());
        settings.CustomHeaders.Add(new AiCustomHeader { Name = "token-issuer", Value = "keycloak" });
        ConfigData config = new();

        config.SetAiSettings(settings);
        AiSettings readSettings = config.GetAiSettings();
        Dictionary<string, string?> configValues = new() { [AiSettings.kCustomHeadersKey] = config.AiCustomHeaders };

        Assert.Multiple(() =>
        {
            Assert.That(config.AiCustomHeaders, Is.EqualTo(kStoredCustomHeaders));
            Assert.That(readSettings.CustomHeaders.Select(header => header.Name), Is.EqualTo(kCustomHeaderNames));
            Assert.That(readSettings.CustomHeaders.Select(header => header.Value), Is.EqualTo(kCustomHeaderValues));
            Assert.That(readSettings.CustomHeaders[0], Is.Not.SameAs(settings.CustomHeaders[0]));
            Assert.That(AiSettings.FromConfigValues(configValues).CustomHeaders.Select(header => header.Name), Is.EqualTo(kCustomHeaderNames));
        });
    }

    /// <summary>
    /// Verifies that the settings page gets unreadable additional headers as none and without a problem, so that storing
    /// the settings replaces them.
    /// </summary>
    [Test]
    public void ConfigExtensions_WithUnreadableCustomHeaders_ReadNone()
    {
        ConfigData config = new();
        config.SetAiSettings(CreateValidSettings(AiProvider.OpenAi));
        config.AiCustomHeaders = "{not json";

        AiSettings settings = config.GetAiSettings();

        Assert.That(settings.CustomHeaders, Is.Empty);
        Assert.That(settings.Validate(), Is.Empty);
    }

    /// <summary>
    /// Verifies that only the referenced credential counts as used, and no credential if none is referenced.
    /// </summary>
    [Test]
    public void AiSettingsUseCredential_MatchesReferencedCredentialOnly()
    {
        ConfigData config = new() { AiCredentialId = kCredentialId };
        ConfigData withoutCredential = new();

        Assert.Multiple(() =>
        {
            Assert.That(config.AiSettingsUseCredential(kCredentialId), Is.True);
            Assert.That(config.AiSettingsUseCredential(kCredentialId + 1), Is.False);
            Assert.That(withoutCredential.AiSettingsUseCredential(0), Is.False);
        });
    }

    /// <summary>
    /// Verifies that every problem has its own error text, and that an unknown problem is refused.
    /// </summary>
    [Test]
    public void ProblemTexts_MapEachProblemToOwnText()
    {
        List<string> textKeys = [.. Enum.GetValues<AiSettingsProblem>().Select(AiSettingsProblemTexts.GetTextKey)];

        Assert.That(textKeys, Is.Unique);
        Assert.That(textKeys, Has.All.StartsWith("E53"));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiSettingsProblemTexts.GetTextKey((AiSettingsProblem)999));
    }

    /// <summary>
    /// Creates valid settings of the given provider.
    /// </summary>
    private static AiSettings CreateValidSettings(AiProvider provider)
    {
        return new AiSettings
        {
            Provider = provider,
            Model = kModel,
            CredentialId = kCredentialId,
            ContextWindowTokens = kContextWindowTokens
        };
    }

    /// <summary>
    /// Creates the config values of valid settings of OpenAI, as the harness reads them.
    /// </summary>
    private static Dictionary<string, string?> CreateValidConfigValues()
    {
        return new Dictionary<string, string?>
        {
            [AiSettings.kProviderKey] = nameof(AiProvider.OpenAi),
            [AiSettings.kModelKey] = kModel,
            [AiSettings.kCredentialIdKey] = kCredentialId.ToString(),
            [AiSettings.kContextWindowTokensKey] = kContextWindowTokens.ToString()
        };
    }

    /// <summary>
    /// Creates valid settings of an OpenAI compatible server with OAuth 2.0.
    /// </summary>
    private static AiSettings CreateValidOAuthSettings()
    {
        AiSettings settings = CreateValidSettings(AiProvider.OpenAi);
        settings.AuthMethod = AiAuthMethod.OAuth2ClientCredentials;
        settings.Endpoint = kHttpsEndpoint;
        settings.TokenUrl = kTokenUrl;
        return settings;
    }
}
