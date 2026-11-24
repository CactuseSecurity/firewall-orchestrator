using FWO.Ai.Harness.Providers;
using FWO.Data.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using OllamaSharp;
using System.Text.Json;
using OllamaOption = OllamaSharp.Models.OllamaOption;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the creation of the provider chat clients and the validation of the provider settings. No provider is
/// called, apart from a stub of the Ollama chat API on a loopback port.
/// </summary>
[TestFixture]
internal class HarnessChatClientFactoryTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-harness-test-key-4711";
    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private const string kOllamaChatPath = "/api/chat";
    private const string kOllamaAnswer = "stub answer";
    private const string kOllamaOptionsProperty = "options";
    private const string kNumCtxProperty = "num_ctx";
    private const string kNumPredictProperty = "num_predict";
    private const string kOtherAdditionalProperty = "fwo_test_property";
    private const string kOtherAdditionalValue = "unchanged";
    private const string kPrompt = "Hello";
    private const string kOpenAiBasePath = "v1/";
    private const string kOpenAiChatPath = "/v1/chat/completions";
    private const string kOpenAiMaxTokensProperty = "max_completion_tokens";
    private const string kOpenAiAnswer = "openai stub answer";
    private const string kJsonContentType = "application/json";
    private const string kOpenAiResponse = """
        {
          "id": "chatcmpl-stub",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "test-model",
          "choices": [ { "index": 0, "message": { "role": "assistant", "content": "openai stub answer" }, "finish_reason": "stop" } ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;
    private const int kContextWindowTokens = 8192;
    private const int kRequestContextWindowTokens = 2048;
    private const int kConfiguredMaxOutputTokens = 123;
    private const int kExplicitMaxOutputTokens = 45;

    private static readonly List<string> kOtherAdditionalPropertyKeys = [kOtherAdditionalProperty];

    /// <summary>
    /// Verifies that each provider gets a chat client for the configured model, which can be disposed.
    /// </summary>
    [TestCase(AiProvider.Ollama, "ollama")]
    [TestCase(AiProvider.OpenAi, "openai")]
    [TestCase(AiProvider.Anthropic, "anthropic")]
    [TestCase(AiProvider.Google, "gcp.gen_ai")]
    public void Create_ForProvider_ReturnsClientOfModel(AiProvider provider, string expectedProviderName)
    {
        AiProviderSettings settings = CreateSettings(provider);

        IChatClient chatClient = ChatClientFactory.Create(settings);

        ChatClientMetadata? metadata = chatClient.GetService<ChatClientMetadata>();
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.ProviderName, Is.EqualTo(expectedProviderName));
        Assert.That(metadata.DefaultModelId, Is.EqualTo(kModel));
        Assert.DoesNotThrow(chatClient.Dispose);
    }

    /// <summary>
    /// Verifies that each provider gets a chat client for a custom endpoint and with a maximum of output tokens.
    /// </summary>
    [TestCase(AiProvider.Ollama, "http://ollama.example.com:11434/")]
    [TestCase(AiProvider.OpenAi, "https://llm.example.com/v1")]
    [TestCase(AiProvider.Anthropic, "https://anthropic.example.com/")]
    [TestCase(AiProvider.Google, "https://gemini.example.com/")]
    public void Create_WithEndpointAndMaxOutputTokens_ReturnsClient(AiProvider provider, string endpoint)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.Endpoint = new Uri(endpoint);
        settings.MaxOutputTokens = kConfiguredMaxOutputTokens;

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient.GetService<ChatClientMetadata>()?.DefaultModelId, Is.EqualTo(kModel));
    }

    /// <summary>
    /// Verifies that Ollama needs no API key and defaults to the local Ollama server.
    /// </summary>
    [Test]
    public void Create_OllamaWithoutApiKey_UsesLocalServer()
    {
        AiProviderSettings settings = CreateSettings(AiProvider.Ollama);
        settings.ApiKey = null;

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient.GetService<ChatClientMetadata>()?.ProviderUri, Is.EqualTo(new Uri(ChatClientFactory.kDefaultOllamaEndpoint)));
    }

    /// <summary>
    /// Verifies that missing settings are refused.
    /// </summary>
    [Test]
    public void Create_WithoutSettings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChatClientFactory.Create(null!));
    }

    /// <summary>
    /// Verifies that a blank model is refused.
    /// </summary>
    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithBlankModel_Throws(string model)
    {
        AiProviderSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.Model = model;

        Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that the cloud providers require an API key.
    /// </summary>
    [TestCase(AiProvider.OpenAi, null)]
    [TestCase(AiProvider.Anthropic, "")]
    [TestCase(AiProvider.Google, " ")]
    public void Create_CloudProviderWithoutApiKey_Throws(AiProvider provider, string? apiKey)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.ApiKey = apiKey;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings))!;

        Assert.That(exception.Message, Does.Contain(provider.ToString()));
    }

    /// <summary>
    /// Verifies that a maximum of output tokens that is not positive is refused.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Create_WithNonPositiveMaxOutputTokens_Throws(int maxOutputTokens)
    {
        AiProviderSettings settings = CreateSettings(AiProvider.Ollama);
        settings.MaxOutputTokens = maxOutputTokens;

        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that a missing (0) or negative context window is refused for every provider.
    /// </summary>
    [TestCase(AiProvider.Ollama, 0)]
    [TestCase(AiProvider.OpenAi, 0)]
    [TestCase(AiProvider.Anthropic, -1)]
    [TestCase(AiProvider.Google, -1)]
    public void Create_WithNonPositiveContextWindow_Throws(AiProvider provider, int contextWindowTokens)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.ContextWindowTokens = contextWindowTokens;

        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that a maximum of output tokens that does not fit into the context window is refused.
    /// </summary>
    [TestCase(0)]
    [TestCase(1)]
    public void Create_WithMaxOutputTokensNotBelowContextWindow_Throws(int tokensAboveContextWindow)
    {
        AiProviderSettings settings = CreateSettings(AiProvider.Ollama);
        settings.MaxOutputTokens = kContextWindowTokens + tokensAboveContextWindow;

        Assert.Throws<ArgumentOutOfRangeException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that the largest maximum of output tokens below the context window is accepted.
    /// </summary>
    [Test]
    public void Create_WithMaxOutputTokensJustBelowContextWindow_ReturnsClient()
    {
        AiProviderSettings settings = CreateSettings(AiProvider.Ollama);
        settings.MaxOutputTokens = kContextWindowTokens - 1;

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient, Is.Not.Null);
    }

    /// <summary>
    /// Verifies that an undefined provider is refused.
    /// </summary>
    [Test]
    public void Create_WithUndefinedProvider_Throws()
    {
        AiProviderSettings settings = CreateSettings((AiProvider)99);

        Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that endpoints which are relative or use another scheme than http and https are refused.
    /// </summary>
    [TestCase(AiProvider.Ollama, "/api")]
    [TestCase(AiProvider.Ollama, "ftp://ollama.example.com/")]
    [TestCase(AiProvider.OpenAi, "ftp://llm.example.com/")]
    public void Create_WithInvalidEndpoint_Throws(AiProvider provider, string endpoint)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.Endpoint = new Uri(endpoint, UriKind.RelativeOrAbsolute);

        Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that the API key is not sent over plain http to a remote host, and that the error message
    /// contains neither the key nor the endpoint.
    /// </summary>
    [TestCase(AiProvider.OpenAi)]
    [TestCase(AiProvider.Anthropic)]
    [TestCase(AiProvider.Google)]
    public void Create_WithApiKeyAndRemoteHttpEndpoint_ThrowsWithoutSecret(AiProvider provider)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.Endpoint = new Uri("http://llm.example.com/v1");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings))!;

        Assert.That(exception.Message, Does.Not.Contain(kApiKey));
        Assert.That(exception.Message, Does.Not.Contain("llm.example.com"));
    }

    /// <summary>
    /// Verifies that the API key may be sent over plain http to a loopback address.
    /// </summary>
    [Test]
    public void Create_WithApiKeyAndLoopbackHttpEndpoint_ReturnsClient()
    {
        AiProviderSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.Endpoint = new Uri("http://127.0.0.1:8000/v1");

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient, Is.Not.Null);
    }

    /// <summary>
    /// Verifies that Ollama, which gets no API key, may use plain http to a remote host.
    /// </summary>
    [Test]
    public void Create_OllamaWithRemoteHttpEndpoint_ReturnsClient()
    {
        AiProviderSettings settings = CreateSettings(AiProvider.Ollama);
        settings.Endpoint = new Uri("http://ollama.example.com:11434/");

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient.GetService<ChatClientMetadata>()?.ProviderUri?.Host, Is.EqualTo("ollama.example.com"));
    }

    /// <summary>
    /// Verifies that request defaults are only added where there is one: for Ollama always (the context window),
    /// for the other providers only with a maximum of output tokens.
    /// </summary>
    [TestCase(AiProvider.Ollama, null, true)]
    [TestCase(AiProvider.OpenAi, null, false)]
    [TestCase(AiProvider.Anthropic, null, false)]
    [TestCase(AiProvider.OpenAi, kConfiguredMaxOutputTokens, true)]
    public void Create_AddsRequestDefaultsOnlyIfThereAreAny(AiProvider provider, int? maxOutputTokens, bool expectRequestDefaults)
    {
        AiProviderSettings settings = CreateSettings(provider);
        settings.MaxOutputTokens = maxOutputTokens;

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient.GetService<ConfigureOptionsChatClient>() != null, Is.EqualTo(expectRequestDefaults));
    }

    /// <summary>
    /// Verifies that the configured maximum of output tokens and the context window are sent with a request that sets neither.
    /// </summary>
    [Test]
    public async Task Create_WithMaxOutputTokens_AppliesItToRequestWithoutMaximum()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, kConfiguredMaxOutputTokens));

        ChatResponse response = await chatClient.GetResponseAsync(kPrompt);

        Assert.That(response.Text, Is.EqualTo(kOllamaAnswer));
        Assert.That(GetOllamaOption(requests.Single(), kNumPredictProperty), Is.EqualTo(kConfiguredMaxOutputTokens));
        Assert.That(GetOllamaOption(requests.Single(), kNumCtxProperty), Is.EqualTo(kContextWindowTokens));
    }

    /// <summary>
    /// Verifies that a maximum of output tokens set on the request is kept, and that the caller's options are not changed.
    /// </summary>
    [Test]
    public async Task Create_WithMaxOutputTokens_KeepsMaximumOfRequest()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, kConfiguredMaxOutputTokens));
        ChatOptions requestOptions = new() { MaxOutputTokens = kExplicitMaxOutputTokens };

        await chatClient.GetResponseAsync(kPrompt, requestOptions);

        Assert.That(GetOllamaOption(requests.Single(), kNumPredictProperty), Is.EqualTo(kExplicitMaxOutputTokens));
        Assert.That(requestOptions.AdditionalProperties, Is.Null);
    }

    /// <summary>
    /// Verifies that every Ollama request carries the context window as num_ctx even without a maximum of output
    /// tokens, as Ollama otherwise cuts the prompt at its own default.
    /// </summary>
    [Test]
    public async Task Create_Ollama_SendsContextWindowAsNumCtx()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, null));

        await chatClient.GetResponseAsync(kPrompt);
        await chatClient.GetResponseAsync(kPrompt);

        Assert.That(requests.Select(request => GetOllamaOption(request, kNumCtxProperty)), Has.All.EqualTo(kContextWindowTokens));
        Assert.That(HasOllamaOption(requests[0], kNumPredictProperty), Is.False);
    }

    /// <summary>
    /// Verifies that a num_ctx set on the request is kept and the caller's options are left as they were.
    /// </summary>
    [Test]
    public async Task Create_Ollama_KeepsNumCtxOfRequest()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, kConfiguredMaxOutputTokens));
        ChatOptions requestOptions = new ChatOptions().AddOllamaOption(OllamaOption.NumCtx, kRequestContextWindowTokens);

        await chatClient.GetResponseAsync(kPrompt, requestOptions);

        Assert.That(GetOllamaOption(requests.Single(), kNumCtxProperty), Is.EqualTo(kRequestContextWindowTokens));
        Assert.That(requestOptions.MaxOutputTokens, Is.Null);
        Assert.That(requestOptions.AdditionalProperties!.Count, Is.EqualTo(1));
        Assert.That(requestOptions.AdditionalProperties[kNumCtxProperty], Is.EqualTo(kRequestContextWindowTokens));
    }

    /// <summary>
    /// Verifies that a num_ctx of null on the request, which OllamaSharp ignores, is replaced by the context window in
    /// the request sent, without changing the caller's options.
    /// </summary>
    [Test]
    public async Task Create_Ollama_WithNullNumCtxOfRequest_SendsContextWindow()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, null));
        ChatOptions requestOptions = new ChatOptions().AddOllamaOption(OllamaOption.NumCtx, null);

        await chatClient.GetResponseAsync(kPrompt, requestOptions);

        Assert.That(GetOllamaOption(requests.Single(), kNumCtxProperty), Is.EqualTo(kContextWindowTokens));
        Assert.That(requestOptions.AdditionalProperties![kNumCtxProperty], Is.Null);
    }

    /// <summary>
    /// Verifies that the context window is added to a copy of the options of a request with other additional
    /// properties, leaving the caller's dictionary unchanged.
    /// </summary>
    [Test]
    public async Task Create_Ollama_WithOtherAdditionalProperties_LeavesCallerDictionaryUnchanged()
    {
        List<JsonElement> requests = [];
        await using WebApplication ollamaStub = await StartOllamaStub(requests);
        using IChatClient chatClient = ChatClientFactory.Create(CreateOllamaStubSettings(ollamaStub, null));
        AdditionalPropertiesDictionary additionalProperties = new() { [kOtherAdditionalProperty] = kOtherAdditionalValue };
        ChatOptions requestOptions = new() { AdditionalProperties = additionalProperties };

        await chatClient.GetResponseAsync(kPrompt, requestOptions);

        Assert.That(GetOllamaOption(requests.Single(), kNumCtxProperty), Is.EqualTo(kContextWindowTokens));
        Assert.That(requestOptions.AdditionalProperties, Is.SameAs(additionalProperties));
        Assert.That(additionalProperties.Keys, Is.EquivalentTo(kOtherAdditionalPropertyKeys));
    }

    /// <summary>
    /// Verifies that a provider other than Ollama gets the configured maximum of output tokens, but not the Ollama
    /// specific num_ctx.
    /// </summary>
    [Test]
    public async Task Create_OpenAiWithMaxOutputTokens_SendsMaximumWithoutNumCtx()
    {
        List<JsonElement> requests = [];
        await using WebApplication openAiStub = await StartProviderStub(requests, kOpenAiChatPath, () => Results.Text(kOpenAiResponse, kJsonContentType));
        AiProviderSettings settings = CreateSettings(AiProvider.OpenAi);
        settings.Endpoint = new Uri(McpTestServer.GetBaseAddress(openAiStub), kOpenAiBasePath);
        settings.MaxOutputTokens = kConfiguredMaxOutputTokens;
        using IChatClient chatClient = ChatClientFactory.Create(settings);

        ChatResponse response = await chatClient.GetResponseAsync(kPrompt);

        Assert.That(response.Text, Is.EqualTo(kOpenAiAnswer));
        JsonElement request = requests.Single();
        Assert.That(request.GetProperty(kOpenAiMaxTokensProperty).GetInt32(), Is.EqualTo(kConfiguredMaxOutputTokens));
        Assert.That(request.GetRawText(), Does.Not.Contain(kNumCtxProperty));
    }

    /// <summary>
    /// Creates valid settings of the given provider with the context window of the tests.
    /// </summary>
    private static AiProviderSettings CreateSettings(AiProvider provider)
    {
        return new AiProviderSettings { Provider = provider, Model = kModel, ApiKey = kApiKey, ContextWindowTokens = kContextWindowTokens };
    }

    /// <summary>
    /// Creates the settings of an Ollama client using the stub, with the given maximum of output tokens.
    /// </summary>
    private static AiProviderSettings CreateOllamaStubSettings(WebApplication ollamaStub, int? maxOutputTokens)
    {
        return new AiProviderSettings
        {
            Provider = AiProvider.Ollama,
            Model = kModel,
            Endpoint = McpTestServer.GetBaseAddress(ollamaStub),
            ContextWindowTokens = kContextWindowTokens,
            MaxOutputTokens = maxOutputTokens
        };
    }

    /// <summary>
    /// Starts a stub of the Ollama chat API on a free loopback port, recording the request bodies.
    /// </summary>
    private static Task<WebApplication> StartOllamaStub(List<JsonElement> requests)
    {
        return StartProviderStub(requests, kOllamaChatPath, () => Results.Json(new
        {
            model = kModel,
            created_at = DateTime.UtcNow,
            message = new { role = "assistant", content = kOllamaAnswer },
            done = true,
            done_reason = "stop"
        }));
    }

    /// <summary>
    /// Starts a stub of the chat API of a provider on a free loopback port, answering requests to the given path with
    /// the given response and recording the request bodies.
    /// </summary>
    private static async Task<WebApplication> StartProviderStub(List<JsonElement> requests, string chatPath, Func<IResult> createResponse)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
        WebApplication app = builder.Build();
        app.MapPost(chatPath, async (HttpContext context) =>
        {
            using JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body);
            lock (requests)
            {
                requests.Add(request.RootElement.Clone());
            }
            return createResponse();
        });
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Returns an integer option of an Ollama chat request (options.&lt;name&gt;).
    /// </summary>
    private static int GetOllamaOption(JsonElement request, string optionName)
    {
        return request.GetProperty(kOllamaOptionsProperty).GetProperty(optionName).GetInt32();
    }

    /// <summary>
    /// Tells whether an Ollama chat request sets an option to a value other than null.
    /// </summary>
    private static bool HasOllamaOption(JsonElement request, string optionName)
    {
        JsonElement options = request.GetProperty(kOllamaOptionsProperty);
        bool isPresent = options.TryGetProperty(optionName, out JsonElement value);
        return isPresent && value.ValueKind != JsonValueKind.Null;
    }
}
