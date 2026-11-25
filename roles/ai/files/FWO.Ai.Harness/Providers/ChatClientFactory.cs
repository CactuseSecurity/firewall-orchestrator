using Anthropic;
using FWO.Basics;
using FWO.Basics.OAuth;
using FWO.Data.Ai;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using AnthropicClientOptions = Anthropic.Core.ClientOptions;
using OllamaOption = OllamaSharp.Models.OllamaOption;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Creates the Microsoft.Extensions.AI chat client of an AI provider.
/// </summary>
public static class ChatClientFactory
{
    /// <summary>Endpoint of a local Ollama server, used if no endpoint is given.</summary>
    internal const string kDefaultOllamaEndpoint = "http://localhost:11434";

    private const string kProviderEndpointPurpose = "AI provider endpoint";
    private const string kTokenUrlPurpose = "token URL";

    private static readonly TimeSpan kMaxRequestTimeout = TimeSpan.FromSeconds(AiSettings.kMaxRequestTimeoutSeconds);

    /// <summary>
    /// Creates the chat client of the configured provider. Disposing the returned client frees the SDK client too.
    /// </summary>
    /// <param name="settings">Provider, model, credentials and token limits</param>
    /// <exception cref="ArgumentException">The settings are incomplete or invalid; the message never contains the API key.</exception>
    public static IChatClient Create(AiProviderSettings settings)
    {
        Validate(settings);
        IChatClient providerClient = settings.Provider switch
        {
            AiProvider.Ollama => CreateOllamaClient(settings),
            AiProvider.OpenAi => CreateOpenAiClient(settings),
            AiProvider.Anthropic => CreateAnthropicClient(settings),
            _ => CreateGoogleClient(settings)
        };
        return AddRequestDefaults(providerClient, settings);
    }

    /// <summary>
    /// Checks the settings before any client is created.
    /// </summary>
    /// <exception cref="ArgumentException">The settings are incomplete or invalid.</exception>
    internal static void Validate(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Provider))
        {
            throw new ArgumentException("Unknown AI provider.", nameof(settings));
        }
        if (settings.Provider == AiProvider.None)
        {
            throw new ArgumentException("No AI provider is configured.", nameof(settings));
        }
        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new ArgumentException("A model is required.", nameof(settings));
        }
        ValidateTokenLimits(settings);
        ValidateRequestTimeout(settings);
        ValidateCustomHeaders(settings);
        if (settings.OAuthClient != null)
        {
            ValidateOAuth(settings, settings.OAuthClient);
            return;
        }
        bool sendsApiKey = settings.Provider != AiProvider.Ollama;
        if (sendsApiKey && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new ArgumentException($"An API key is required for {settings.Provider}.", nameof(settings));
        }
        if (settings.Endpoint == null)
        {
            return;
        }
        if (sendsApiKey)
        {
            EndpointGuard.RequireHttpsOrLoopback(settings.Endpoint, kProviderEndpointPurpose);
        }
        else
        {
            EndpointGuard.RequireHttpOrHttps(settings.Endpoint, kProviderEndpointPurpose);
        }
    }

    /// <summary>
    /// Checks the settings of OAuth 2.0: only for an OpenAI compatible server, which needs an endpoint, and both the
    /// endpoint (it gets the access token) and the token URL (it gets the client secret) must use https unless they are
    /// a loopback address.
    /// </summary>
    /// <exception cref="ArgumentException">OAuth 2.0 cannot be used with these settings.</exception>
    private static void ValidateOAuth(AiProviderSettings settings, OAuthClientCredentials oauthClient)
    {
        if (settings.Provider != AiProvider.OpenAi)
        {
            throw new ArgumentException($"OAuth 2.0 is only supported for {AiProvider.OpenAi}.", nameof(settings));
        }
        if (settings.Endpoint == null)
        {
            throw new ArgumentException("OAuth 2.0 needs the endpoint of an OpenAI compatible server.", nameof(settings));
        }
        EndpointGuard.RequireHttpsOrLoopback(settings.Endpoint, kProviderEndpointPurpose);
        EndpointGuard.RequireHttpsOrLoopback(oauthClient.TokenUrl, kTokenUrlPurpose);
    }

    /// <summary>
    /// Checks the context window and the maximum number of output tokens, which has to fit into the context window.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A token limit is not positive, or the output does not fit.</exception>
    private static void ValidateTokenLimits(AiProviderSettings settings)
    {
        if (settings.ContextWindowTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The context window must be positive.");
        }
        if (settings.MaxOutputTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The maximum number of output tokens must be positive.");
        }
        if (settings.MaxOutputTokens is int maxOutputTokens && maxOutputTokens >= settings.ContextWindowTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The maximum number of output tokens must be smaller than the context window.");
        }
    }

    /// <summary>
    /// Checks the request timeout: positive and at most <see cref="AiSettings.kMaxRequestTimeoutSeconds"/> seconds, which
    /// also keeps it within the milliseconds the Google client takes as int.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The request timeout is not positive or too long.</exception>
    private static void ValidateRequestTimeout(AiProviderSettings settings)
    {
        if (settings.RequestTimeout <= TimeSpan.Zero || settings.RequestTimeout > kMaxRequestTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(settings),
                $"The request timeout must be positive and at most {AiSettings.kMaxRequestTimeoutSeconds} seconds.");
        }
    }

    /// <summary>
    /// Checks the additional headers like the AI settings do, so that they can neither replace the headers carrying the
    /// API key or the access token nor end the header line.
    /// </summary>
    /// <exception cref="ArgumentException">The headers are invalid; the message names the problems, not the values.</exception>
    private static void ValidateCustomHeaders(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.CustomHeaders, nameof(settings));
        List<AiCustomHeader> customHeaders = [.. settings.CustomHeaders.Select(header => new AiCustomHeader { Name = header.Key, Value = header.Value })];
        List<AiSettingsProblem> problems = AiCustomHeaderList.FindProblems(customHeaders);
        if (problems.Count > 0)
        {
            throw new ArgumentException($"The additional headers are invalid: {string.Join(", ", problems)}.", nameof(settings));
        }
    }

    /// <summary>
    /// Adds the defaults every request gets unless it sets them itself: the maximum number of output tokens, if
    /// configured, and for Ollama the context window. Returns the provider client unchanged if there is no default.
    /// </summary>
    private static IChatClient AddRequestDefaults(IChatClient providerClient, AiProviderSettings settings)
    {
        int? maxOutputTokens = settings.MaxOutputTokens;
        int? ollamaContextWindowTokens = settings.Provider == AiProvider.Ollama ? settings.ContextWindowTokens : null;
        if (maxOutputTokens == null && ollamaContextWindowTokens == null)
        {
            return providerClient;
        }
        return new ChatClientBuilder(providerClient)
            .ConfigureOptions(options => ApplyRequestDefaults(options, maxOutputTokens, ollamaContextWindowTokens))
            .Build();
    }

    /// <summary>
    /// Sets the defaults on the options of a request (a copy made by the options configuring client), keeping what the
    /// request sets itself.
    /// </summary>
    /// <param name="options">Options of the request</param>
    /// <param name="maxOutputTokens">Default maximum number of output tokens; none if null</param>
    /// <param name="ollamaContextWindowTokens">Context window sent to Ollama as num_ctx; none if null</param>
    private static void ApplyRequestDefaults(ChatOptions options, int? maxOutputTokens, int? ollamaContextWindowTokens)
    {
        options.MaxOutputTokens ??= maxOutputTokens;
        if (ollamaContextWindowTokens is int numCtx && !HasOllamaOption(options, OllamaOption.NumCtx))
        {
            options.AddOllamaOption(OllamaOption.NumCtx, numCtx);
        }
    }

    /// <summary>
    /// Tells whether the options already set an Ollama option; OllamaSharp ignores an option whose value is null.
    /// </summary>
    private static bool HasOllamaOption(ChatOptions options, OllamaOption option)
    {
        object? value = null;
        bool isSet = options.AdditionalProperties?.TryGetValue(option.Name, out value) == true;
        return isSet && value != null;
    }

    /// <summary>
    /// Returns the API key of a provider that requires one, checked by <see cref="Validate"/> before.
    /// </summary>
    /// <exception cref="ArgumentException">There is no API key.</exception>
    private static string GetApiKey(AiProviderSettings settings)
    {
        return settings.ApiKey ?? throw new ArgumentException($"An API key is required for {settings.Provider}.", nameof(settings));
    }

    /// <summary>
    /// Creates an Ollama client with the request timeout and the additional headers; the OllamaSharp client is a chat
    /// client itself. OllamaSharp takes the timeout only with an HTTP client of the caller, which it does not dispose,
    /// so the returned client owns that HTTP client and disposes it.
    /// </summary>
    private static IChatClient CreateOllamaClient(AiProviderSettings settings)
    {
        HttpClient httpClient = CreateOllamaHttpClient(settings);
        OllamaApiClient ollamaClient = new(httpClient, settings.Model);
        foreach (KeyValuePair<string, string> header in settings.CustomHeaders)
        {
            ollamaClient.DefaultRequestHeaders[header.Key] = header.Value;
        }
        return new ProviderChatClient(ollamaClient, httpClient);
    }

    /// <summary>
    /// Creates the HTTP client of the Ollama client, for the endpoint (the local Ollama server if none is given) and with
    /// the request timeout instead of the default of 100 seconds. Ollama answers a request that is not streamed, e.g.
    /// the summary of a long history, only once the whole answer is generated, so the timeout bounds the generation.
    /// </summary>
    internal static HttpClient CreateOllamaHttpClient(AiProviderSettings settings)
    {
        return new HttpClient
        {
            BaseAddress = settings.Endpoint ?? new Uri(kDefaultOllamaEndpoint),
            Timeout = settings.RequestTimeout
        };
    }

    /// <summary>
    /// Creates an OpenAI client with the request timeout and the additional headers, for a custom OpenAI compatible
    /// endpoint if one is given; with the access token of OAuth 2.0 if configured, else with the API key.
    /// </summary>
    private static IChatClient CreateOpenAiClient(AiProviderSettings settings)
    {
        OpenAIClientOptions clientOptions = CreateOpenAiClientOptions(settings);
        if (settings.OAuthClient != null)
        {
            return CreateOpenAiOAuthClient(settings.Model, settings.OAuthClient, clientOptions);
        }
        OpenAIClient openAiClient = new(new ApiKeyCredential(GetApiKey(settings)), clientOptions);
        return openAiClient.GetChatClient(settings.Model).AsIChatClient();
    }

    /// <summary>
    /// Returns the options of the OpenAI client: the request timeout instead of the default of 100 seconds (per attempt,
    /// for a streamed answer per read of the stream), the custom endpoint if one is given, and the additional headers.
    /// </summary>
    internal static OpenAIClientOptions CreateOpenAiClientOptions(AiProviderSettings settings)
    {
        OpenAIClientOptions clientOptions = new() { NetworkTimeout = settings.RequestTimeout };
        if (settings.Endpoint != null)
        {
            clientOptions.Endpoint = settings.Endpoint;
        }
        if (settings.CustomHeaders.Count > 0)
        {
            clientOptions.AddPolicy(new CustomHeadersPolicy(settings.CustomHeaders), PipelinePosition.PerCall);
        }
        return clientOptions;
    }

    /// <summary>
    /// Creates an OpenAI client that sends an access token of OAuth 2.0 instead of an API key. The token is kept until
    /// shortly before it expires; its provider, with the connections to the token endpoint, is disposed with the client.
    /// The certificate of the token endpoint is always checked.
    /// </summary>
    private static IChatClient CreateOpenAiOAuthClient(string model, OAuthClientCredentials oauthClient, OpenAIClientOptions clientOptions)
    {
        CachedOAuthTokenProvider tokenProvider = new(new OAuthTokenClient(), oauthClient);
        // the constructor taking an authentication policy is experimental in the OpenAI SDK 2.14 (OPENAI001); it is the
        // only way to authenticate without an API key, and a change in a later SDK version shows up when building
#pragma warning disable OPENAI001
        OpenAIClient openAiClient = new(new OAuthBearerPolicy(tokenProvider), clientOptions);
#pragma warning restore OPENAI001
        IChatClient chatClient = openAiClient.GetChatClient(model).AsIChatClient();
        return new ProviderChatClient(chatClient, tokenProvider);
    }

    /// <summary>
    /// Creates an Anthropic client with the request timeout and the additional headers, for a custom base URL if one is
    /// given.
    /// </summary>
    private static IChatClient CreateAnthropicClient(AiProviderSettings settings)
    {
        AnthropicClient anthropicClient = new(CreateAnthropicClientOptions(settings));
        IChatClient chatClient = anthropicClient.AsIChatClient(settings.Model, settings.MaxOutputTokens);
        return new ProviderChatClient(chatClient, anthropicClient);
    }

    /// <summary>
    /// Returns the options of the Anthropic client: the API key, the additional headers, which the SDK adds after the
    /// authentication headers and never lets them replace these, the request timeout (per attempt) and the custom base
    /// URL if one is given. Without a timeout of its own, the SDK derives the timeout of a request that is not streamed,
    /// e.g. the summary of a long history, from its maximum number of output tokens: 30 seconds per 1000 tokens, so only
    /// 30 seconds for the default of 1024 tokens, and for Claude Opus 4 and 4.1 it refuses such a request with more than
    /// 8192 tokens.
    /// </summary>
    internal static AnthropicClientOptions CreateAnthropicClientOptions(AiProviderSettings settings)
    {
        AnthropicClientOptions clientOptions = new()
        {
            ApiKey = GetApiKey(settings),
            ExtraHeaders = new Dictionary<string, string>(settings.CustomHeaders),
            Timeout = settings.RequestTimeout
        };
        // BaseUrl must only be set if a custom one is given, otherwise the SDK default applies
        if (settings.Endpoint != null)
        {
            clientOptions.BaseUrl = settings.Endpoint.ToString();
        }
        return clientOptions;
    }

    /// <summary>
    /// Creates a Google client for the Gemini Developer API (not Vertex AI), with the request timeout, and for a custom
    /// base URL and with the additional headers if given.
    /// </summary>
    private static IChatClient CreateGoogleClient(AiProviderSettings settings)
    {
        Google.GenAI.Client googleClient = new(vertexAI: false, apiKey: GetApiKey(settings), httpOptions: CreateGoogleHttpOptions(settings));
        IChatClient chatClient = googleClient.AsIChatClient(settings.Model);
        return new ProviderChatClient(chatClient, googleClient);
    }

    /// <summary>
    /// Returns the HTTP options of the Google client: the request timeout in milliseconds, without which the SDK waits
    /// without limit (for a streamed answer it bounds the whole answer), and the custom base URL and the additional
    /// headers, each only if given, so that the SDK defaults apply otherwise.
    /// </summary>
    internal static HttpOptions CreateGoogleHttpOptions(AiProviderSettings settings)
    {
        HttpOptions httpOptions = new() { Timeout = (int)Math.Ceiling(settings.RequestTimeout.TotalMilliseconds) };
        if (settings.Endpoint != null)
        {
            httpOptions.BaseUrl = settings.Endpoint.ToString();
        }
        if (settings.CustomHeaders.Count > 0)
        {
            httpOptions.Headers = new Dictionary<string, string>(settings.CustomHeaders);
        }
        return httpOptions;
    }
}
