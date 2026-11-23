using System.Globalization;

namespace FWO.Data.Ai
{
    /// <summary>
    /// Problems <see cref="AiSettings.Validate"/> finds in the AI settings.
    /// </summary>
    public enum AiSettingsProblem
    {
        /// <summary>No model is given.</summary>
        ModelMissing,

        /// <summary>The provider needs an API key, but no credential is selected.</summary>
        CredentialMissing,

        /// <summary>The endpoint is not an absolute http or https URL.</summary>
        EndpointInvalid,

        /// <summary>The API key would be sent over plain http to a host that is not a loopback address.</summary>
        EndpointNotSecure,

        /// <summary>The context window is not positive.</summary>
        ContextWindowInvalid,

        /// <summary>The maximum number of output tokens is negative or does not fit into the context window.</summary>
        MaxOutputTokensInvalid,

        /// <summary>The maximum number of model requests per turn is smaller than 1.</summary>
        MaxIterationsInvalid,

        /// <summary>The maximum number of consecutive failed tool call rounds is negative.</summary>
        MaxConsecutiveErrorsInvalid,

        /// <summary>The compaction reserve is negative, does not fit into the context window, or does not hold a full answer.</summary>
        CompactionReserveInvalid,

        /// <summary>OAuth 2.0 is selected for a provider other than OpenAI (an OpenAI compatible server).</summary>
        OAuthNotSupported,

        /// <summary>OAuth 2.0 is selected without an endpoint; the public OpenAI API does not accept such tokens.</summary>
        OAuthEndpointMissing,

        /// <summary>The token URL of OAuth 2.0 is missing, not absolute, or plain http to a host that is not a loopback address.</summary>
        TokenUrlInvalid
    }

    /// <summary>
    /// Settings of the AI provider, of the agent loop of the AI harness (FWO.Ai.Harness) and the switches and role filters
    /// of the AI functions offered to the users, stored as global config items (config_user 0) under the keys below.
    /// Edited on the AI settings page of the UI and read by the AI harness with its technical user, which refuses the runs
    /// of a function that is switched off or not allowed for the roles of the user. Secrets are not part of the settings:
    /// they reference a credential of the credential
    /// store (<see cref="CredentialId"/>), whose secret is the API key, or for OAuth 2.0 (<see cref="AuthMethod"/>) whose
    /// user name is the client id and whose secret is the client secret.
    /// </summary>
    public class AiSettings
    {
        /// <summary>Config key of <see cref="Provider"/>.</summary>
        public const string kProviderKey = "aiProvider";

        /// <summary>Config key of <see cref="Model"/>.</summary>
        public const string kModelKey = "aiModel";

        /// <summary>Config key of <see cref="Endpoint"/>.</summary>
        public const string kEndpointKey = "aiEndpoint";

        /// <summary>
        /// Config key of <see cref="CredentialId"/>. The database view v_ai_provider_credential returns the credential
        /// it names, keep both in step.
        /// </summary>
        public const string kCredentialIdKey = "aiCredentialId";

        /// <summary>Config key of <see cref="ContextWindowTokens"/>.</summary>
        public const string kContextWindowTokensKey = "aiContextWindowTokens";

        /// <summary>Config key of <see cref="MaxOutputTokens"/>.</summary>
        public const string kMaxOutputTokensKey = "aiMaxOutputTokens";

        /// <summary>Config key of <see cref="MaxIterationsPerRequest"/>.</summary>
        public const string kMaxIterationsPerRequestKey = "aiMaxIterationsPerRequest";

        /// <summary>Config key of <see cref="MaxConsecutiveErrorsPerRequest"/>.</summary>
        public const string kMaxConsecutiveErrorsPerRequestKey = "aiMaxConsecutiveErrorsPerRequest";

        /// <summary>Config key of <see cref="CompactionReserveTokens"/>.</summary>
        public const string kCompactionReserveTokensKey = "aiCompactionReserveTokens";

        /// <summary>Config key of <see cref="AuthMethod"/>.</summary>
        public const string kAuthMethodKey = "aiAuthMethod";

        /// <summary>Config key of <see cref="TokenUrl"/>.</summary>
        public const string kTokenUrlKey = "aiTokenUrl";

        /// <summary>Config key of <see cref="ChatEnabled"/>.</summary>
        public const string kChatKey = "aiChat";

        /// <summary>Config key of <see cref="ReasonCheckEnabled"/>.</summary>
        public const string kReasonCheckKey = "aiReasonCheck";

        /// <summary>Config key of <see cref="ChatRoles"/>.</summary>
        public const string kChatRolesKey = "aiChatRoles";

        /// <summary>Config key of <see cref="ReasonCheckRoles"/>.</summary>
        public const string kReasonCheckRolesKey = "aiReasonCheckRoles";

        /// <summary>
        /// Default maximum number of model requests offering the tools per turn. Lower than the default of
        /// Microsoft.Extensions.AI (40), to bound the cost of a single turn.
        /// </summary>
        public const int kDefaultMaxIterationsPerRequest = 10;

        /// <summary>Default maximum number of consecutive failed tool call rounds before a turn fails.</summary>
        public const int kDefaultMaxConsecutiveErrorsPerRequest = 3;

        /// <summary>Without an explicit compaction reserve, the reserve is the context window divided by this.</summary>
        public const int kDefaultCompactionReserveDivisor = 4;

        private const int kMinIterationsPerRequest = 1;

        /// <summary>
        /// All config keys of the AI settings. The Hasura permission of the ai-harness role lets it read exactly these
        /// keys of the global config, keep both in step.
        /// </summary>
        public static readonly IReadOnlyList<string> kKeys =
        [
            kProviderKey, kModelKey, kEndpointKey, kCredentialIdKey, kContextWindowTokensKey, kMaxOutputTokensKey,
            kMaxIterationsPerRequestKey, kMaxConsecutiveErrorsPerRequestKey, kCompactionReserveTokensKey, kAuthMethodKey, kTokenUrlKey,
            kChatKey, kReasonCheckKey, kChatRolesKey, kReasonCheckRolesKey
        ];

        /// <summary>Provider to talk to; <see cref="AiProvider.None"/> if the AI functions are not available.</summary>
        public AiProvider Provider { get; set; } = AiProvider.None;

        /// <summary>Model id, e.g. the model name known to the provider.</summary>
        public string Model { get; set; } = "";

        /// <summary>Custom base URL of the provider, e.g. an OpenAI compatible server; the provider's default if empty.</summary>
        public string Endpoint { get; set; } = "";

        /// <summary>
        /// Id of the credential whose secret is the API key, or for OAuth 2.0 whose user name is the client id and whose
        /// secret is the client secret; 0 if none. Required for all providers but Ollama.
        /// </summary>
        public int CredentialId { get; set; }

        /// <summary>How the harness authenticates at the provider: with an API key, or with an OAuth 2.0 access token.</summary>
        public AiAuthMethod AuthMethod { get; set; } = AiAuthMethod.ApiKey;

        /// <summary>
        /// Absolute URL of the token endpoint for OAuth 2.0 (client credentials grant), e.g. of a Keycloak realm; only
        /// used with <see cref="AiAuthMethod.OAuth2ClientCredentials"/>.
        /// </summary>
        public string TokenUrl { get; set; } = "";

        /// <summary>Context window of the model in tokens; required and positive.</summary>
        public int ContextWindowTokens { get; set; }

        /// <summary>Default maximum number of output tokens per response; the provider's default if 0.</summary>
        public int MaxOutputTokens { get; set; }

        /// <summary>Maximum number of model requests offering the tools per turn, the initial request included.</summary>
        public int MaxIterationsPerRequest { get; set; } = kDefaultMaxIterationsPerRequest;

        /// <summary>Maximum number of consecutive failed tool call rounds before a turn fails.</summary>
        public int MaxConsecutiveErrorsPerRequest { get; set; } = kDefaultMaxConsecutiveErrorsPerRequest;

        /// <summary>
        /// Tokens of the context window kept free of the conversation before the older part of it is summarized;
        /// a quarter of the context window if 0.
        /// </summary>
        public int CompactionReserveTokens { get; set; }

        /// <summary>
        /// Whether the users may chat with the AI assistant: ask questions of their own and answer its tool approval
        /// requests. Off by default.
        /// </summary>
        public bool ChatEnabled { get; set; }

        /// <summary>
        /// Whether modellers may let the AI assistant check the functional reason of a connection, an interface or an
        /// interface usage. Off by default.
        /// </summary>
        public bool ReasonCheckEnabled { get; set; }

        /// <summary>
        /// Roles allowed to chat with the AI assistant, see <see cref="AiFunctionRoles"/>; every user if empty.
        /// </summary>
        public List<string> ChatRoles { get; set; } = [];

        /// <summary>
        /// Roles allowed to let the AI assistant check the functional reason, see <see cref="AiFunctionRoles"/>; every
        /// user if empty.
        /// </summary>
        public List<string> ReasonCheckRoles { get; set; } = [];

        /// <summary>Whether an AI provider is configured.</summary>
        public bool IsConfigured => Provider != AiProvider.None;

        /// <summary>Whether the provider needs a credential: the API key, or the client id and secret for OAuth 2.0.</summary>
        public bool RequiresCredential => IsConfigured && Provider != AiProvider.Ollama;

        /// <summary>Whether the harness authenticates with an OAuth 2.0 access token instead of an API key.</summary>
        public bool UsesOAuth => IsConfigured && AuthMethod == AiAuthMethod.OAuth2ClientCredentials;

        /// <summary>Whether the provider needs an API key: a credential, used without OAuth 2.0.</summary>
        public bool RequiresApiKey => RequiresCredential && !UsesOAuth;

        /// <summary>
        /// Returns the compaction reserve in effect: <see cref="CompactionReserveTokens"/>, or a quarter of the context
        /// window if that is 0.
        /// </summary>
        public int GetEffectiveCompactionReserveTokens()
        {
            return CompactionReserveTokens > 0 ? CompactionReserveTokens : ContextWindowTokens / kDefaultCompactionReserveDivisor;
        }

        /// <summary>
        /// Reads the settings from global config items. A missing or unreadable value keeps its default, which the
        /// validation reports where the default is not valid (e.g. a missing context window).
        /// </summary>
        /// <param name="configValues">Config values by config key, e.g. read with the query getAiSettings</param>
        /// <returns>The settings.</returns>
        public static AiSettings FromConfigValues(IReadOnlyDictionary<string, string?> configValues)
        {
            ArgumentNullException.ThrowIfNull(configValues);
            AiSettings settings = new();
            settings.Provider = ReadEnum(configValues, kProviderKey, settings.Provider);
            settings.Model = ReadText(configValues, kModelKey);
            settings.Endpoint = ReadText(configValues, kEndpointKey);
            settings.CredentialId = ReadNumber(configValues, kCredentialIdKey, settings.CredentialId);
            settings.AuthMethod = ReadEnum(configValues, kAuthMethodKey, settings.AuthMethod);
            settings.TokenUrl = ReadText(configValues, kTokenUrlKey);
            settings.ContextWindowTokens = ReadNumber(configValues, kContextWindowTokensKey, settings.ContextWindowTokens);
            settings.MaxOutputTokens = ReadNumber(configValues, kMaxOutputTokensKey, settings.MaxOutputTokens);
            settings.MaxIterationsPerRequest = ReadNumber(configValues, kMaxIterationsPerRequestKey, settings.MaxIterationsPerRequest);
            settings.MaxConsecutiveErrorsPerRequest = ReadNumber(configValues, kMaxConsecutiveErrorsPerRequestKey, settings.MaxConsecutiveErrorsPerRequest);
            settings.CompactionReserveTokens = ReadNumber(configValues, kCompactionReserveTokensKey, settings.CompactionReserveTokens);
            (settings.ChatEnabled, settings.ChatRoles) = ReadFunction(configValues, kChatKey, kChatRolesKey);
            (settings.ReasonCheckEnabled, settings.ReasonCheckRoles) = ReadFunction(configValues, kReasonCheckKey, kReasonCheckRolesKey);
            return settings;
        }

        /// <summary>
        /// Tells whether a user holding the given roles may chat with the AI assistant: the chat is switched on and the
        /// user holds one of the <see cref="ChatRoles"/>, if any are set.
        /// </summary>
        /// <param name="userRoles">Roles of the user</param>
        public bool MayUseChat(IEnumerable<string> userRoles)
        {
            return ChatEnabled && AiFunctionRoles.IsAllowed(ChatRoles, userRoles);
        }

        /// <summary>
        /// Tells whether a user holding the given roles may run the reason check: it is switched on and the user holds
        /// one of the <see cref="ReasonCheckRoles"/>, if any are set.
        /// </summary>
        /// <param name="userRoles">Roles of the user</param>
        public bool MayUseReasonCheck(IEnumerable<string> userRoles)
        {
            return ReasonCheckEnabled && AiFunctionRoles.IsAllowed(ReasonCheckRoles, userRoles);
        }

        /// <summary>
        /// Checks the settings the way the AI harness does before it uses them. Settings without a provider have no
        /// problems, as they are not used.
        /// </summary>
        /// <returns>The problems found, empty if the settings are valid.</returns>
        public List<AiSettingsProblem> Validate()
        {
            List<AiSettingsProblem> problems = [];
            if (!IsConfigured)
            {
                return problems;
            }
            if (string.IsNullOrWhiteSpace(Model))
            {
                problems.Add(AiSettingsProblem.ModelMissing);
            }
            if (RequiresCredential && CredentialId <= 0)
            {
                problems.Add(AiSettingsProblem.CredentialMissing);
            }
            AddEndpointProblem(problems);
            AddOAuthProblems(problems);
            AddTokenLimitProblems(problems);
            AddAgentLoopProblems(problems);
            return problems;
        }

        /// <summary>
        /// Adds the problems of OAuth 2.0, if it is used: only for an OpenAI compatible server, which needs an endpoint,
        /// and with a token URL that is absolute and uses https unless it is a loopback address, as the client secret is
        /// sent to it.
        /// </summary>
        private void AddOAuthProblems(List<AiSettingsProblem> problems)
        {
            if (!UsesOAuth)
            {
                return;
            }
            if (Provider != AiProvider.OpenAi)
            {
                problems.Add(AiSettingsProblem.OAuthNotSupported);
            }
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                problems.Add(AiSettingsProblem.OAuthEndpointMissing);
            }
            if (!IsSecretSafeUrl(TokenUrl))
            {
                problems.Add(AiSettingsProblem.TokenUrlInvalid);
            }
        }

        /// <summary>
        /// Tells whether a URL a secret may be sent to is an absolute https URL, or an http URL of a loopback address.
        /// </summary>
        private static bool IsSecretSafeUrl(string url)
        {
            bool isAbsolute = Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri);
            if (!isAbsolute || uri == null)
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
        }

        /// <summary>
        /// Adds the problem of the endpoint, if any: it must be an absolute http or https URL, and must use https
        /// unless it is a loopback address if a secret (the API key or the access token) is sent to it.
        /// </summary>
        private void AddEndpointProblem(List<AiSettingsProblem> problems)
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                return;
            }
            bool isAbsolute = Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out Uri? endpoint);
            if (!isAbsolute || endpoint == null || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                problems.Add(AiSettingsProblem.EndpointInvalid);
            }
            else if (RequiresCredential && endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
            {
                problems.Add(AiSettingsProblem.EndpointNotSecure);
            }
        }

        /// <summary>
        /// Adds the problems of the context window and of the maximum number of output tokens, which has to fit into it.
        /// </summary>
        private void AddTokenLimitProblems(List<AiSettingsProblem> problems)
        {
            if (ContextWindowTokens <= 0)
            {
                problems.Add(AiSettingsProblem.ContextWindowInvalid);
            }
            if (MaxOutputTokens < 0 || (MaxOutputTokens > 0 && MaxOutputTokens >= ContextWindowTokens))
            {
                problems.Add(AiSettingsProblem.MaxOutputTokensInvalid);
            }
        }

        /// <summary>
        /// Adds the problems of the agent loop and of the compaction reserve, which must be smaller than the context
        /// window and larger than the maximum number of output tokens, so that a full answer fits.
        /// </summary>
        private void AddAgentLoopProblems(List<AiSettingsProblem> problems)
        {
            if (MaxIterationsPerRequest < kMinIterationsPerRequest)
            {
                problems.Add(AiSettingsProblem.MaxIterationsInvalid);
            }
            if (MaxConsecutiveErrorsPerRequest < 0)
            {
                problems.Add(AiSettingsProblem.MaxConsecutiveErrorsInvalid);
            }
            int reserveTokens = GetEffectiveCompactionReserveTokens();
            bool reserveFits = reserveTokens > 0 && reserveTokens < ContextWindowTokens;
            bool reserveHoldsAnswer = MaxOutputTokens <= 0 || reserveTokens > MaxOutputTokens;
            if (CompactionReserveTokens < 0 || (ContextWindowTokens > 0 && !(reserveFits && reserveHoldsAnswer)))
            {
                problems.Add(AiSettingsProblem.CompactionReserveInvalid);
            }
        }

        /// <summary>
        /// Reads an enum value by name, ignoring case; the default for a missing, empty, numeric or unknown name.
        /// </summary>
        private static TEnum ReadEnum<TEnum>(IReadOnlyDictionary<string, string?> configValues, string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            string text = ReadText(configValues, key);
            bool isKnown = Enum.TryParse(text, ignoreCase: true, out TEnum value) && Enum.IsDefined(value);
            return isKnown && !int.TryParse(text, out _) ? value : defaultValue;
        }

        /// <summary>
        /// Reads a text, trimmed; empty if missing.
        /// </summary>
        private static string ReadText(IReadOnlyDictionary<string, string?> configValues, string key)
        {
            return configValues.TryGetValue(key, out string? value) && value != null ? value.Trim() : "";
        }

        /// <summary>
        /// Reads an integer; the default for a missing or unreadable value.
        /// </summary>
        private static int ReadNumber(IReadOnlyDictionary<string, string?> configValues, string key, int defaultValue)
        {
            string text = ReadText(configValues, key);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : defaultValue;
        }

        /// <summary>
        /// Reads a switch stored as True or False, ignoring case; off for a missing or unreadable value, so that a
        /// function is only offered once it is switched on.
        /// </summary>
        private static bool ReadSwitch(IReadOnlyDictionary<string, string?> configValues, string key)
        {
            string text = ReadText(configValues, key);
            return bool.TryParse(text, out bool switchedOn) && switchedOn;
        }

        /// <summary>
        /// Reads the switch and the role filter of an AI function. A missing role filter restricts nothing; an
        /// unreadable one switches the function off, so that it is not offered to every user by mistake.
        /// </summary>
        private static (bool Enabled, List<string> Roles) ReadFunction(IReadOnlyDictionary<string, string?> configValues,
            string switchKey, string rolesKey)
        {
            bool rolesReadable = AiFunctionRoles.TryParse(ReadText(configValues, rolesKey), out List<string> roles);
            return (ReadSwitch(configValues, switchKey) && rolesReadable, roles);
        }
    }
}
