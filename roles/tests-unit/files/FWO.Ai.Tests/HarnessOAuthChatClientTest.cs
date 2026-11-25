using FWO.Ai.Harness.Providers;
using FWO.Basics.Exceptions;
using FWO.Basics.OAuth;
using FWO.Data.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests OAuth 2.0 of the OpenAI client: the chat client gets its access token from the token endpoint, keeps it, sends
/// it as bearer token instead of an API key, and renews a refused token once. A stub on a loopback port stands in for
/// both the token endpoint and the OpenAI compatible server.
/// </summary>
[TestFixture]
internal class HarnessOAuthChatClientTest
{
    private const string kModel = "test-model";
    private const string kClientId = "fwo-ai-client";
    private const string kClientSecret = "oauth-client-secret-4711";
    private const string kFirstToken = "access-token-1";
    private const string kSecondToken = "access-token-2";
    private const string kPrompt = "Hello";
    private const string kOpenAiAnswer = "oauth stub answer";
    private const string kTokenPath = "/realms/fwo/protocol/openid-connect/token";
    private const string kOpenAiBasePath = "v1/";
    private const string kOpenAiChatPath = "/v1/chat/completions";
    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private const string kJsonContentType = "application/json";
    private const string kHttpsEndpoint = "https://llm.example.test/v1";
    private const string kHttpsTokenUrl = "https://sso.example.test/realms/fwo/protocol/openid-connect/token";
    private const int kContextWindowTokens = 8192;
    private const long kTokenLifetimeSeconds = 3600;
    private const string kOpenAiResponse = """
        {
          "id": "chatcmpl-stub",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "test-model",
          "choices": [ { "index": 0, "message": { "role": "assistant", "content": "oauth stub answer" }, "finish_reason": "stop" } ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;

    private static readonly List<string?> kFirstTokenTwice = [$"Bearer {kFirstToken}", $"Bearer {kFirstToken}"];
    private static readonly List<string?> kFirstThenSecondToken = [$"Bearer {kFirstToken}", $"Bearer {kSecondToken}"];
    private static readonly List<string> kIssuedTokens = [kFirstToken, kSecondToken];

    /// <summary>
    /// Verifies that the access token is requested with the client credentials, sent as bearer token, and kept for the
    /// next request.
    /// </summary>
    [Test]
    public async Task Create_WithOAuth_SendsTheAccessTokenAndKeepsIt()
    {
        OAuthStub stub = new();
        stub.AnswerToken(kFirstToken);
        await using WebApplication app = await stub.Start();
        using IChatClient chatClient = ChatClientFactory.Create(CreateStubSettings(app));

        ChatResponse first = await chatClient.GetResponseAsync(kPrompt);
        ChatResponse second = await chatClient.GetResponseAsync(kPrompt);

        Assert.Multiple(() =>
        {
            Assert.That(first.Text, Is.EqualTo(kOpenAiAnswer));
            Assert.That(second.Text, Is.EqualTo(kOpenAiAnswer));
            Assert.That(stub.ChatAuthorizations, Is.EqualTo(kFirstTokenTwice));
            Assert.That(stub.TokenRequests, Has.Count.EqualTo(1));
            Assert.That(stub.TokenRequests[0], Does.Contain("grant_type=client_credentials"));
            Assert.That(stub.TokenRequests[0], Does.Contain($"client_id={kClientId}"));
            Assert.That(stub.TokenRequests[0], Does.Contain($"client_secret={kClientSecret}"));
        });
    }

    /// <summary>
    /// Verifies that a token the server refuses is dropped and the request is sent once more with a new token.
    /// </summary>
    [Test]
    public async Task Create_WithOAuth_RenewsARefusedTokenOnce()
    {
        OAuthStub stub = new();
        stub.AnswerToken(kFirstToken);
        stub.AnswerToken(kSecondToken);
        stub.ChatStatuses.Enqueue(StatusCodes.Status401Unauthorized);
        await using WebApplication app = await stub.Start();
        using IChatClient chatClient = ChatClientFactory.Create(CreateStubSettings(app));

        ChatResponse response = await chatClient.GetResponseAsync(kPrompt);

        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo(kOpenAiAnswer));
            Assert.That(stub.ChatAuthorizations, Is.EqualTo(kFirstThenSecondToken));
            Assert.That(stub.TokenRequests, Has.Count.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies that a renewed token that is refused as well fails the request instead of trying again and again.
    /// </summary>
    [Test]
    public async Task Create_WithOAuth_RefusedRenewedToken_Fails()
    {
        OAuthStub stub = new();
        stub.AnswerToken(kFirstToken);
        stub.AnswerToken(kSecondToken);
        stub.ChatStatuses.Enqueue(StatusCodes.Status401Unauthorized);
        stub.ChatStatuses.Enqueue(StatusCodes.Status401Unauthorized);
        await using WebApplication app = await stub.Start();
        using IChatClient chatClient = ChatClientFactory.Create(CreateStubSettings(app));

        ClientResultException? exception = Assert.CatchAsync<ClientResultException>(() => chatClient.GetResponseAsync(kPrompt));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Status, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(stub.ChatAuthorizations, Has.Count.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies that a token endpoint refusing the client credentials fails the request before the server is called,
    /// without revealing the client secret.
    /// </summary>
    [Test]
    public async Task Create_WithOAuth_RefusedClientCredentials_FailWithoutSecret()
    {
        OAuthStub stub = new();
        stub.TokenAnswers.Enqueue(() => Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized));
        await using WebApplication app = await stub.Start();
        using IChatClient chatClient = ChatClientFactory.Create(CreateStubSettings(app));

        Exception? exception = Assert.CatchAsync<Exception>(() => chatClient.GetResponseAsync(kPrompt));

        OAuthTokenException? tokenException = FindException<OAuthTokenException>(exception);
        Assert.Multiple(() =>
        {
            Assert.That(tokenException, Is.Not.Null, exception?.ToString());
            Assert.That(tokenException?.Error, Is.EqualTo("invalid_client"));
            Assert.That(exception?.ToString(), Does.Not.Contain(kClientSecret));
            Assert.That(stub.ChatAuthorizations, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that OAuth 2.0 needs no API key.
    /// </summary>
    [Test]
    public void Create_WithOAuthAndWithoutApiKey_ReturnsClient()
    {
        using IChatClient chatClient = ChatClientFactory.Create(CreateSettings(kHttpsEndpoint, kHttpsTokenUrl));

        Assert.That(chatClient.GetService<ChatClientMetadata>()?.ProviderName, Is.EqualTo("openai"));
    }

    /// <summary>
    /// Verifies that OAuth 2.0 is refused for providers other than OpenAI.
    /// </summary>
    [TestCase(AiProvider.Anthropic)]
    [TestCase(AiProvider.Google)]
    [TestCase(AiProvider.Ollama)]
    public void Create_WithOAuthForOtherProvider_Throws(AiProvider provider)
    {
        AiProviderSettings settings = CreateSettings(kHttpsEndpoint, kHttpsTokenUrl);
        settings.Provider = provider;

        Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that OAuth 2.0 needs the endpoint of an OpenAI compatible server.
    /// </summary>
    [Test]
    public void Create_WithOAuthWithoutEndpoint_Throws()
    {
        AiProviderSettings settings = CreateSettings(kHttpsEndpoint, kHttpsTokenUrl);
        settings.Endpoint = null;

        Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(settings));
    }

    /// <summary>
    /// Verifies that neither the access token nor the client secret is sent over plain http to a remote host, and that
    /// the message names neither the host nor the secret.
    /// </summary>
    [TestCase("http://llm.example.test/v1", kHttpsTokenUrl)]
    [TestCase(kHttpsEndpoint, "http://sso.example.test/token")]
    public void Create_WithOAuthAndRemoteHttp_ThrowsWithoutSecret(string endpoint, string tokenUrl)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => ChatClientFactory.Create(CreateSettings(endpoint, tokenUrl)))!;

        Assert.That(exception.Message, Does.Not.Contain("example.test"));
        Assert.That(exception.Message, Does.Not.Contain(kClientSecret));
    }

    /// <summary>
    /// Verifies that the policy refuses to send the token over plain http to a remote host, before asking for a token.
    /// </summary>
    [Test]
    public void Policy_PlainHttpToRemoteHost_IsRefused()
    {
        RecordingHandler server = new(HttpStatusCode.OK);
        (ClientPipeline pipeline, CachedOAuthTokenProvider tokenProvider, RecordingHandler tokenEndpoint) = CreatePipeline(server);
        using (tokenProvider)
        {
            using PipelineMessage message = CreateMessage(pipeline, "http://llm.example.test/v1/models");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.SendAsync(message));
            Assert.That(tokenEndpoint.Requests, Is.Empty);
            Assert.That(server.Requests, Is.Empty);
        }
    }

    /// <summary>
    /// Verifies that the synchronous path of the SDK sets the token as well, and renews a refused one.
    /// </summary>
    [Test]
    public void Policy_SynchronousSend_RenewsARefusedToken()
    {
        RecordingHandler server = new(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        (ClientPipeline pipeline, CachedOAuthTokenProvider tokenProvider, _) = CreatePipeline(server);
        using (tokenProvider)
        {
            using PipelineMessage message = CreateMessage(pipeline, "http://127.0.0.1:1/v1/models");

            pipeline.Send(message);

            Assert.That(message.Response?.Status, Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(server.Requests, Is.EqualTo(kFirstThenSecondToken));
        }
    }

    /// <summary>
    /// Verifies that the policy needs a token provider.
    /// </summary>
    [Test]
    public void Policy_WithoutTokenProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new OAuthBearerPolicy(null!));
    }

    /// <summary>
    /// Creates OAuth settings of the OpenAI client for the given endpoint and token URL.
    /// </summary>
    private static AiProviderSettings CreateSettings(string endpoint, string tokenUrl)
    {
        return new AiProviderSettings
        {
            Provider = AiProvider.OpenAi,
            Model = kModel,
            Endpoint = new Uri(endpoint),
            OAuthClient = new OAuthClientCredentials(new Uri(tokenUrl), kClientId, kClientSecret),
            ContextWindowTokens = kContextWindowTokens
        };
    }

    /// <summary>
    /// Creates OAuth settings of the OpenAI client pointing to the stub.
    /// </summary>
    private static AiProviderSettings CreateStubSettings(WebApplication app)
    {
        Uri baseAddress = McpTestServer.GetBaseAddress(app);
        return CreateSettings(new Uri(baseAddress, kOpenAiBasePath).ToString(), new Uri(baseAddress, kTokenPath).ToString());
    }

    /// <summary>
    /// Creates a pipeline with the policy, sending to the given server, whose token provider gets its tokens from a
    /// recording token endpoint answering two tokens.
    /// </summary>
    private static (ClientPipeline Pipeline, CachedOAuthTokenProvider TokenProvider, RecordingHandler TokenEndpoint) CreatePipeline(RecordingHandler server)
    {
        RecordingHandler tokenEndpoint = new(HttpStatusCode.OK, HttpStatusCode.OK)
        {
            TokenAnswers = new Queue<string>(kIssuedTokens)
        };
        CachedOAuthTokenProvider tokenProvider = new(new OAuthTokenClient(tokenEndpoint), new OAuthClientCredentials(new Uri(kHttpsTokenUrl), kClientId, kClientSecret));
        ClientPipelineOptions options = new()
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(server)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };
        PipelinePolicy[] perTryPolicies = [new OAuthBearerPolicy(tokenProvider)];
        ClientPipeline pipeline = ClientPipeline.Create(options, ReadOnlySpan<PipelinePolicy>.Empty, perTryPolicies, ReadOnlySpan<PipelinePolicy>.Empty);
        return (pipeline, tokenProvider, tokenEndpoint);
    }

    /// <summary>
    /// Creates a GET request of the pipeline to the given URI.
    /// </summary>
    private static PipelineMessage CreateMessage(ClientPipeline pipeline, string uri)
    {
        PipelineMessage message = pipeline.CreateMessage();
        message.Request.Method = HttpMethod.Get.Method;
        message.Request.Uri = new Uri(uri);
        return message;
    }

    /// <summary>
    /// Returns the exception of the given type in the exception or its inner exceptions; null if there is none.
    /// </summary>
    private static TException? FindException<TException>(Exception? exception) where TException : Exception
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is TException found)
            {
                return found;
            }
            if (current is AggregateException aggregate)
            {
                return aggregate.InnerExceptions.Select(FindException<TException>).FirstOrDefault(inner => inner != null);
            }
        }
        return null;
    }

    /// <summary>
    /// Stub of the token endpoint and the chat API of an OpenAI compatible server on a free loopback port, recording the
    /// token requests and the authorization of the chat requests.
    /// </summary>
    private sealed class OAuthStub
    {
        internal Queue<Func<IResult>> TokenAnswers { get; } = new();

        internal Queue<int> ChatStatuses { get; } = new();

        internal List<string> TokenRequests { get; } = [];

        internal List<string?> ChatAuthorizations { get; } = [];

        internal void AnswerToken(string accessToken)
        {
            TokenAnswers.Enqueue(() => Results.Json(new { access_token = accessToken, token_type = "Bearer", expires_in = kTokenLifetimeSeconds }));
        }

        internal async Task<WebApplication> Start()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
            WebApplication app = builder.Build();
            app.MapPost(kTokenPath, async (HttpContext context) =>
            {
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync();
                lock (TokenRequests)
                {
                    TokenRequests.Add(body);
                    return TokenAnswers.Dequeue()();
                }
            });
            app.MapPost(kOpenAiChatPath, (HttpContext context) =>
            {
                lock (ChatAuthorizations)
                {
                    ChatAuthorizations.Add(context.Request.Headers.Authorization.ToString());
                    int status = ChatStatuses.Count > 0 ? ChatStatuses.Dequeue() : StatusCodes.Status200OK;
                    return status == StatusCodes.Status200OK
                        ? Results.Text(kOpenAiResponse, kJsonContentType)
                        : Results.Json(new { error = new { message = "invalid token" } }, statusCode: status);
                }
            });
            await app.StartAsync();
            return app;
        }
    }

    /// <summary>
    /// Answers requests with the given status codes in turn, recording their authorization; as token endpoint it answers
    /// the queued tokens.
    /// </summary>
    private sealed class RecordingHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> statuses = new(statusCodes);

        internal Queue<string>? TokenAnswers { get; init; }

        internal List<string?> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Headers.Authorization?.ToString());
            string content = TokenAnswers == null ? "{}" : $"{{\"access_token\":\"{TokenAnswers.Dequeue()}\",\"expires_in\":{kTokenLifetimeSeconds}}}";
            return Task.FromResult(new HttpResponseMessage(statuses.Dequeue()) { Content = new StringContent(content) });
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        }
    }
}
