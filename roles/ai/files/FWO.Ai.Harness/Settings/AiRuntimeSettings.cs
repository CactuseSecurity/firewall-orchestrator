using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Providers;
using FWO.Basics.OAuth;
using FWO.Data.Ai;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// AI settings the harness runs its agent with: the settings of the AI settings page of the UI, the secret from the
/// credential they reference (the API key, or the client secret of OAuth 2.0 with the client id), or why they cannot be
/// used.
/// A class rather than a record on purpose: the generated ToString of a record would print the secret.
/// </summary>
public sealed class AiRuntimeSettings
{
    private const string kEndpointPurpose = "AI provider endpoint";
    private const string kTokenUrlPurpose = "token URL";

    /// <summary>
    /// Creates the runtime settings.
    /// </summary>
    /// <param name="values">Settings of the AI settings page</param>
    /// <param name="secret">
    /// Secret of the credential in plain text: the API key, or the client secret of OAuth 2.0; null if the provider needs
    /// none or it could not be read
    /// </param>
    /// <param name="problem">Why the settings cannot be used, null if they can; never contains the secret</param>
    /// <param name="clientId">Client id of OAuth 2.0, the user name of the credential; null without OAuth 2.0</param>
    public AiRuntimeSettings(AiSettings values, string? secret = null, string? problem = null, string? clientId = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
        Secret = secret;
        Problem = problem;
        ClientId = clientId;
    }

    /// <summary>Settings of the AI settings page.</summary>
    public AiSettings Values { get; }

    /// <summary>Secret of the credential: the API key, or the client secret of OAuth 2.0; null if none. Never logged.</summary>
    public string? Secret { get; }

    /// <summary>Client id of OAuth 2.0; null without OAuth 2.0.</summary>
    public string? ClientId { get; }

    /// <summary>Why the settings cannot be used; null if they can. Never contains the secret.</summary>
    public string? Problem { get; }

    /// <summary>Whether an AI provider is configured.</summary>
    public bool IsConfigured => Values.IsConfigured;

    /// <summary>
    /// Tells whether the other settings have the same values, the secret included, so that the agent factory can be
    /// kept.
    /// </summary>
    /// <param name="other">Settings to compare with; null for none</param>
    public bool HasSameValues(AiRuntimeSettings? other)
    {
        return other != null && GetComparableValues().Equals(other.GetComparableValues());
    }

    /// <summary>
    /// Returns the settings of the AI provider: with the API key, or with the client credentials of OAuth 2.0, and with
    /// the additional headers.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The endpoint or the token URL is not an absolute URI, OAuth 2.0 lacks the client id or secret, or two additional
    /// headers have the same name.
    /// </exception>
    public AiProviderSettings ToProviderSettings()
    {
        return new AiProviderSettings
        {
            Provider = Values.Provider,
            Model = Values.Model,
            ApiKey = Values.RequiresApiKey ? Secret : null,
            OAuthClient = Values.UsesOAuth ? CreateOAuthClient() : null,
            Endpoint = ParseAbsoluteUri(Values.Endpoint, kEndpointPurpose),
            CustomHeaders = CreateCustomHeaders(),
            ContextWindowTokens = Values.ContextWindowTokens,
            MaxOutputTokens = Values.MaxOutputTokens > 0 ? Values.MaxOutputTokens : null
        };
    }

    /// <summary>
    /// Returns the settings of the agent loop, the compaction and the AI functions the users may run, with the roles
    /// allowed to run them.
    /// </summary>
    public AgentSettings ToAgentSettings()
    {
        return new AgentSettings
        {
            MaxIterationsPerRequest = Values.MaxIterationsPerRequest,
            MaxConsecutiveErrorsPerRequest = Values.MaxConsecutiveErrorsPerRequest,
            CompactionReserveTokens = Values.CompactionReserveTokens > 0 ? Values.CompactionReserveTokens : null,
            ChatEnabled = Values.ChatEnabled,
            ReasonCheckEnabled = Values.ReasonCheckEnabled,
            ChatRoles = [.. Values.ChatRoles],
            ReasonCheckRoles = [.. Values.ReasonCheckRoles]
        };
    }

    /// <summary>
    /// Returns the client credentials of OAuth 2.0 from the token URL and the credential.
    /// </summary>
    /// <exception cref="ArgumentException">The token URL is missing or not absolute, or the client id or secret is missing.</exception>
    private OAuthClientCredentials CreateOAuthClient()
    {
        Uri tokenUrl = ParseAbsoluteUri(Values.TokenUrl, kTokenUrlPurpose)
            ?? throw new ArgumentException("A token URL is required for OAuth 2.0.", nameof(Values));
        return new OAuthClientCredentials(tokenUrl, ClientId ?? "", Secret ?? "");
    }

    /// <summary>
    /// Returns the additional headers by name, trimmed and without blank rows.
    /// </summary>
    /// <exception cref="ArgumentException">Two headers have the same name, ignoring case.</exception>
    private Dictionary<string, string> CreateCustomHeaders()
    {
        List<AiCustomHeader> customHeaders = AiCustomHeaderList.Normalize(Values.CustomHeaders);
        return customHeaders.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns an absolute URI, null for an empty text.
    /// </summary>
    /// <exception cref="ArgumentException">The text is not an absolute URI; the text is not part of the message.</exception>
    private static Uri? ParseAbsoluteUri(string text, string purpose)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        return Uri.TryCreate(text.Trim(), UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new ArgumentException($"The {purpose} must be an absolute http or https URI.", nameof(text));
    }

    /// <summary>
    /// Returns all values that decide about the agent factory, for <see cref="HasSameValues"/>; the switches and role
    /// filters of the AI functions too, as the factory hands them to its agents. The role filters and the additional
    /// headers are compared in their normalized stored form, as lists compare by reference.
    /// </summary>
    private (AiProvider, string, string, int, int, int, int, int, int, AiAuthMethod, string, string?, string?, string?, bool, bool, string, string, string) GetComparableValues()
    {
        return (Values.Provider, Values.Model, Values.Endpoint, Values.CredentialId, Values.ContextWindowTokens, Values.MaxOutputTokens,
            Values.MaxIterationsPerRequest, Values.MaxConsecutiveErrorsPerRequest, Values.CompactionReserveTokens, Values.AuthMethod,
            Values.TokenUrl, ClientId, Secret, Problem, Values.ChatEnabled, Values.ReasonCheckEnabled,
            AiFunctionRoles.Serialize(Values.ChatRoles), AiFunctionRoles.Serialize(Values.ReasonCheckRoles),
            AiCustomHeaderList.Serialize(Values.CustomHeaders));
    }
}
