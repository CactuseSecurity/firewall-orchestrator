using FWO.Basics.OAuth;
using FWO.Data.Ai;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Settings of the AI provider and model to use, taken from the AI settings of the UI (see
/// <see cref="Settings.AiRuntimeSettings"/>).
/// A class rather than a record on purpose: the generated ToString of a record would print the API key.
/// </summary>
public class AiProviderSettings
{
    /// <summary>Provider to talk to; <see cref="AiProvider.None"/> is refused when the chat client is created.</summary>
    public AiProvider Provider { get; set; }

    /// <summary>Model id, e.g. the model name known to the provider.</summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// API key; required for all providers but Ollama unless <see cref="OAuthClient"/> is set. Never logged or put into
    /// error messages.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Client credentials of OAuth 2.0: if set, the access token they get is sent as bearer token instead of an API key.
    /// For <see cref="AiProvider.OpenAi"/> with an <see cref="Endpoint"/> only.
    /// </summary>
    public OAuthClientCredentials? OAuthClient { get; set; }

    /// <summary>Custom base URL of the provider; the provider's default if not set.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Additional HTTP headers sent with every request to the provider, by name; none if empty. They cannot replace the
    /// headers carrying the API key or the access token, see <see cref="AiCustomHeaderList"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Context window of the model in tokens; required and positive. The agent compacts its history by it, and every
    /// request to Ollama carries it as num_ctx, as Ollama otherwise cuts the prompt at its own, often much smaller, default.
    /// </summary>
    public int ContextWindowTokens { get; set; }

    /// <summary>
    /// Default maximum number of output tokens per response; the provider's default if not set.
    /// Must be smaller than <see cref="ContextWindowTokens"/>.
    /// </summary>
    public int? MaxOutputTokens { get; set; }
}
