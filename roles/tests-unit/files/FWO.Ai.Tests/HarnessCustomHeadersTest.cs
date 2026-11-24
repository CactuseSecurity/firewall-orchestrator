using FWO.Ai.Harness.Providers;
using FWO.Basics.OAuth;
using FWO.Data.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.ClientModel.Primitives;
using System.Net;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the additional HTTP headers of the AI settings: every provider client sends them with every request, next to
/// its own authentication, and the OAuth 2.0 token request does not get them. Headers that would carry credentials or
/// end the header line are refused. A stub on a loopback port stands in for the providers and the token endpoint.
/// </summary>
[TestFixture]
internal class HarnessCustomHeadersTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-harness-test-key-4711";
    private const string kPrompt = "Hello";
    private const string kAnswer = "stub answer";
    private const string kProfileHeader = "ki-services-profile-name";
    private const string kProfileValue = "com_1659_test";
    private const string kIssuerHeader = "token-issuer";
    private const string kIssuerValue = "keycloak";
    private const string kAuthorizationHeader = "Authorization";
    private const string kGoogleApiKeyHeader = "x-goog-api-key";
    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private const string kCatchAllPath = "/{**path}";
    private const string kTokenPath = "/realms/fwo/protocol/openid-connect/token";
    private const string kOpenAiBasePath = "v1/";
    private const string kJsonContentType = "application/json";
    private const string kAccessToken = "access-token-1";
    private const string kClientId = "fwo-ai-client";
    private const string kClientSecret = "oauth-client-secret-4711";
    private const string kPipelineUri = "http://127.0.0.1:1/v1/models";
    private const string kOutdatedValue = "outdated";
    private const int kContextWindowTokens = 8192;
    private const long kTokenLifetimeSeconds = 3600;
    private const string kOpenAiResponse = """
        {
          "id": "chatcmpl-stub",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "test-model",
          "choices": [ { "index": 0, "message": { "role": "assistant", "content": "stub answer" }, "finish_reason": "stop" } ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;
    private const string kAnthropicResponse = """
        {
          "id": "msg_stub",
          "type": "message",
          "role": "assistant",
          "model": "test-model",
          "content": [ { "type": "text", "text": "stub answer" } ],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": { "input_tokens": 1, "output_tokens": 1 }
        }
        """;
    private const string kGoogleResponse = """
        {
          "candidates": [ { "content": { "role": "model", "parts": [ { "text": "stub answer" } ] }, "finishReason": "STOP", "index": 0 } ],
          "usageMetadata": { "promptTokenCount": 1, "candidatesTokenCount": 1, "totalTokenCount": 2 }
        }
        """;

    private static readonly Dictionary<string, string> kCustomHeaders = new()
    {
        [kProfileHeader] = kProfileValue,
        [kIssuerHeader] = kIssuerValue
    };

    /// <summary>
    /// Verifies that each provider sends the additional headers with its request, and still authenticates with its API
    /// key, which the headers cannot replace.
    /// </summary>
    [TestCase(AiProvider.Ollama, "", "")]
    [TestCase(AiProvider.OpenAi, kOpenAiBasePath, kAuthorizationHeader)]
    [TestCase(AiProvider.Anthropic, "", kAuthorizationHeader)]
    [TestCase(AiProvider.Google, "", kGoogleApiKeyHeader)]
    public async Task Create_WithCustomHeaders_SendsThemToTheProvider(AiProvider provider, string basePath, string apiKeyHeader)
    {
        ProviderStub stub = new(provider);
        await using WebApplication app = await stub.Start();
        AiProviderSettings settings = new()
        {
            Provider = provider,
            Model = kModel,
            ApiKey = provider == AiProvider.Ollama ? null : kApiKey,
            Endpoint = new Uri(McpTestServer.GetBaseAddress(app), basePath),
            ContextWindowTokens = kContextWindowTokens,
            CustomHeaders = kCustomHeaders
        };
        using IChatClient chatClient = ChatClientFactory.Create(settings);

        ChatResponse response = await chatClient.GetResponseAsync(kPrompt);

        Dictionary<string, string> requestHeaders = stub.ChatRequests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo(kAnswer));
            Assert.That(requestHeaders[kProfileHeader], Is.EqualTo(kProfileValue));
            Assert.That(requestHeaders[kIssuerHeader], Is.EqualTo(kIssuerValue));
            if (apiKeyHeader.Length > 0)
            {
                Assert.That(requestHeaders[apiKeyHeader], Does.Contain(kApiKey));
            }
        });
    }

    /// <summary>
    /// Verifies the case of an OpenAI compatible server behind an API gateway with OAuth 2.0: the chat request carries
    /// the additional headers and the access token, the request to the token endpoint gets no additional header.
    /// </summary>
    [Test]
    public async Task Create_WithOAuthAndCustomHeaders_SendsThemToTheChatOnly()
    {
        ProviderStub stub = new(AiProvider.OpenAi);
        await using WebApplication app = await stub.Start();
        Uri baseAddress = McpTestServer.GetBaseAddress(app);
        AiProviderSettings settings = new()
        {
            Provider = AiProvider.OpenAi,
            Model = kModel,
            Endpoint = new Uri(baseAddress, kOpenAiBasePath),
            OAuthClient = new OAuthClientCredentials(new Uri(baseAddress, kTokenPath), kClientId, kClientSecret),
            ContextWindowTokens = kContextWindowTokens,
            CustomHeaders = kCustomHeaders
        };
        using IChatClient chatClient = ChatClientFactory.Create(settings);

        ChatResponse response = await chatClient.GetResponseAsync(kPrompt);

        Dictionary<string, string> chatHeaders = stub.ChatRequests.Single();
        Dictionary<string, string> tokenHeaders = stub.TokenRequests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo(kAnswer));
            Assert.That(chatHeaders[kProfileHeader], Is.EqualTo(kProfileValue));
            Assert.That(chatHeaders[kIssuerHeader], Is.EqualTo(kIssuerValue));
            Assert.That(chatHeaders[kAuthorizationHeader], Is.EqualTo($"Bearer {kAccessToken}"));
            Assert.That(tokenHeaders.Keys, Has.None.EqualTo(kProfileHeader).IgnoreCase);
            Assert.That(tokenHeaders.Keys, Has.None.EqualTo(kIssuerHeader).IgnoreCase);
        });
    }

    /// <summary>
    /// Verifies that a client without additional headers is created as before.
    /// </summary>
    [TestCase(AiProvider.Ollama)]
    [TestCase(AiProvider.OpenAi)]
    [TestCase(AiProvider.Anthropic)]
    [TestCase(AiProvider.Google)]
    public void Create_WithoutCustomHeaders_ReturnsClient(AiProvider provider)
    {
        AiProviderSettings settings = new() { Provider = provider, Model = kModel, ApiKey = kApiKey, ContextWindowTokens = kContextWindowTokens };

        using IChatClient chatClient = ChatClientFactory.Create(settings);

        Assert.That(chatClient, Is.Not.Null);
    }

    /// <summary>
    /// Verifies that headers carrying credentials or framing the message, invalid names and values that could end the
    /// header line are refused before a client is created, without the value in the message.
    /// </summary>
    [TestCase("Authorization", "Bearer injected-secret")]
    [TestCase("X-API-KEY", "injected-secret")]
    [TestCase("x-goog-api-key", "injected-secret")]
    [TestCase("Host", "injected-secret.example.com")]
    [TestCase("profile name", "injected-secret")]
    [TestCase(kProfileHeader, "injected-secret\r\nX-Injected: 1")]
    [TestCase(kProfileHeader, " ")]
    public void Create_WithInvalidCustomHeader_ThrowsWithoutValue(string name, string value)
    {
        AiProviderSettings settings = new()
        {
            Provider = AiProvider.OpenAi,
            Model = kModel,
            ApiKey = kApiKey,
            ContextWindowTokens = kContextWindowTokens,
            CustomHeaders = new Dictionary<string, string> { [name] = value }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings))!;

        Assert.That(exception.Message, Does.Not.Contain("injected-secret"));
    }

    /// <summary>
    /// Verifies that the policy of the OpenAI client sets the headers on the synchronous and the asynchronous path,
    /// replacing a value set before.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task Policy_SetsHeadersOnEveryPath(bool isAsync)
    {
        RecordingHandler server = new();
        ClientPipelineOptions options = new()
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(server)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };
        PipelinePolicy[] perCallPolicies = [new CustomHeadersPolicy(kCustomHeaders)];
        ClientPipeline pipeline = ClientPipeline.Create(options, perCallPolicies, ReadOnlySpan<PipelinePolicy>.Empty, ReadOnlySpan<PipelinePolicy>.Empty);
        using PipelineMessage message = pipeline.CreateMessage();
        message.Request.Method = HttpMethod.Get.Method;
        message.Request.Uri = new Uri(kPipelineUri);
        message.Request.Headers.Set(kProfileHeader, kOutdatedValue);

        if (isAsync)
        {
            await pipeline.SendAsync(message);
        }
        else
        {
            pipeline.Send(message);
        }

        Dictionary<string, string> requestHeaders = server.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(requestHeaders[kProfileHeader], Is.EqualTo(kProfileValue));
            Assert.That(requestHeaders[kIssuerHeader], Is.EqualTo(kIssuerValue));
        });
    }

    /// <summary>
    /// Verifies that the policy needs the headers.
    /// </summary>
    [Test]
    public void Policy_WithoutHeaders_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new CustomHeadersPolicy(null!));
    }

    /// <summary>
    /// Returns the headers of a request by name, ignoring case.
    /// </summary>
    private static Dictionary<string, string> ReadHeaders(IHeaderDictionary headers)
    {
        return headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stub of the chat API of a provider and of an OAuth 2.0 token endpoint on a free loopback port, recording the headers
    /// of the requests.
    /// </summary>
    private sealed class ProviderStub(AiProvider provider)
    {
        internal List<Dictionary<string, string>> ChatRequests { get; } = [];

        internal List<Dictionary<string, string>> TokenRequests { get; } = [];

        internal async Task<WebApplication> Start()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
            WebApplication app = builder.Build();
            app.MapPost(kTokenPath, (HttpContext context) =>
            {
                Record(TokenRequests, context);
                return Results.Json(new { access_token = kAccessToken, token_type = "Bearer", expires_in = kTokenLifetimeSeconds });
            });
            app.MapPost(kCatchAllPath, (HttpContext context) =>
            {
                Record(ChatRequests, context);
                return CreateChatResponse();
            });
            await app.StartAsync();
            return app;
        }

        private static void Record(List<Dictionary<string, string>> requests, HttpContext context)
        {
            lock (requests)
            {
                requests.Add(ReadHeaders(context.Request.Headers));
            }
        }

        private IResult CreateChatResponse()
        {
            return provider switch
            {
                AiProvider.Ollama => Results.Json(new
                {
                    model = kModel,
                    created_at = DateTime.UtcNow,
                    message = new { role = "assistant", content = kAnswer },
                    done = true,
                    done_reason = "stop"
                }),
                AiProvider.OpenAi => Results.Text(kOpenAiResponse, kJsonContentType),
                AiProvider.Anthropic => Results.Text(kAnthropicResponse, kJsonContentType),
                _ => Results.Text(kGoogleResponse, kJsonContentType)
            };
        }
    }

    /// <summary>
    /// Answers every request with an empty JSON object, recording its headers.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal List<Dictionary<string, string>> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        }
    }
}
