using FWO.Ai.Client;
using FWO.Ai.Client.AgUi;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FWO.Test;

/// <summary>
/// Tests the chat clients of the FWO agent with a fake handler: the endpoint guard, the requests of the runs (prompt,
/// conversation, thread and approval answers), the access token, the mapping of refused runs, connection failures,
/// timeouts and cancellation, and the disposal of the HTTP client.
/// </summary>
[TestFixture]
[Parallelizable]
internal class FwoAgentChatClientFactoryTest
{
    private const string kMiddlewareUri = "https://fwo.example:8888/";
    private const string kEndpoint = "https://fwo.example:8888/agui";
    private const string kAccessToken = "header.payload.signature";
    private const string kOtherAccessToken = "other.payload.signature";
    private const string kThreadId = "thread-ui";
    private const string kQuestion = "Which roles do I have?";
    private const string kAnswer = "You are a modeller.";
    private const string kFollowUp = "And which apps?";
    private const string kPromptName = "check_connection_reason";
    private const string kReasonArgument = "reason";
    private const string kReason = "Reads the orders for the monthly export.";
    private const string kInterruptId = "interrupt-1";
    private const string kToolCallId = "call-1";
    private const string kToolName = "store_value";
    private const string kToolArgumentName = "value";
    private const string kToolArgumentValue = "x";
    private const string kFinishedRun = "data: {\"type\":\"RUN_STARTED\",\"threadId\":\"t\",\"runId\":\"r\"}\n\n"
        + "data: {\"type\":\"RUN_FINISHED\",\"threadId\":\"t\",\"runId\":\"r\"}\n\n";
    private const string kBearerScheme = "Bearer";
    private const string kJsonMediaType = "application/json";
    private const string kProtocolVersion = "1.0";
    private const int kRetryAfterSeconds = 30;
    private const int kRetryAfterDateMinutes = 2;
    private const int kShortTimeoutMilliseconds = 100;
    private const string kUserInfoSecret = "s3cret";
    private const string kUserInfoHost = "fwo.example";
    private const string kUriWithUserInfo = "http://admin:" + kUserInfoSecret + "@" + kUserInfoHost + ":8888/";

    private static readonly Uri kMiddlewareServerUri = new(kMiddlewareUri);
    private static readonly List<TimeSpan> kInvalidRunTimeouts = [TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.MaxValue];
    private static readonly IReadOnlyDictionary<string, string> kPromptArguments = new Dictionary<string, string> { [kReasonArgument] = kReason };
    private static readonly List<string> kEventStreamAccept = [AgUiFakeHandler.kEventStreamMediaType];
    private static readonly List<string?> kUnusableTokens = [null, "", " ", "secret token", "token\r\nX-Injected: 1"];
    private static readonly List<ChatMessage> kNoMessages = [];
    private static readonly List<ChatMessage> kQuestionOnly = [new ChatMessage(ChatRole.User, kQuestion)];
    private static readonly Dictionary<string, object?> kToolArguments = new() { [kToolArgumentName] = kToolArgumentValue };

    /// <summary>
    /// Verifies that the AG-UI path is added below the path of the middleware server uri.
    /// </summary>
    [TestCase("https://fwo.example:8888/", "https://fwo.example:8888/agui")]
    [TestCase("https://fwo.example:8888", "https://fwo.example:8888/agui")]
    [TestCase("https://fwo.example:8888/base", "https://fwo.example:8888/base/agui")]
    [TestCase("https://fwo.example:8888/base/", "https://fwo.example:8888/base/agui")]
    [TestCase("http://127.0.0.1:8891/", "http://127.0.0.1:8891/agui")]
    [TestCase("http://localhost/", "http://localhost/agui")]
    [TestCase("http://[::1]:8891/", "http://[::1]:8891/agui")]
    public void Constructor_WithHttpsOrLoopback_UsesAgUiEndpoint(string middlewareServerUri, string expectedEndpoint)
    {
        using FwoAgentChatClientFactory factory = new(new HttpClient(), new Uri(middlewareServerUri));

        Assert.That(factory.Endpoint, Is.EqualTo(new Uri(expectedEndpoint)));
    }

    /// <summary>
    /// Verifies that both constructors refuse an endpoint the token would be sent to in clear text or that is no http
    /// endpoint.
    /// </summary>
    [TestCase("http://fwo.example:8888/")]
    [TestCase("http://10.1.1.1:8888/")]
    [TestCase("ftp://fwo.example/")]
    [TestCase("file:///etc/fworch/")]
    public void Constructor_WithInsecureOrOtherEndpoint_Throws(string middlewareServerUri)
    {
        Uri uri = new(middlewareServerUri);

        Assert.Throws<ArgumentException>(() => new FwoAgentChatClientFactory(uri));
        Assert.Throws<ArgumentException>(() => new FwoAgentChatClientFactory(new HttpClient(), uri));
    }

    /// <summary>
    /// Verifies that the refusal of an endpoint does not name it, as it may contain user information.
    /// </summary>
    [Test]
    public void Constructor_WithInsecureEndpoint_DoesNotNameEndpoint()
    {
        ArgumentException? exception = Assert.Throws<ArgumentException>(() => new FwoAgentChatClientFactory(new Uri(kUriWithUserInfo)));

        Assert.That(exception?.Message, Does.Not.Contain(kUserInfoSecret));
        Assert.That(exception?.Message, Does.Not.Contain(kUserInfoHost));
    }

    /// <summary>
    /// Verifies that a relative uri and missing arguments are refused.
    /// </summary>
    [Test]
    public void Constructor_WithRelativeUriOrNullArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FwoAgentChatClientFactory(new HttpClient(),
            new Uri(FwoAgentChatClientFactory.kAgUiPath, UriKind.Relative)));
        Assert.Throws<ArgumentNullException>(() => new FwoAgentChatClientFactory(null!));
        Assert.Throws<ArgumentNullException>(() => new FwoAgentChatClientFactory(null!, kMiddlewareServerUri));
        Assert.Throws<ArgumentNullException>(() => new FwoAgentChatClientFactory(new HttpClient(), null!));
    }

    /// <summary>
    /// Verifies that a run timeout that is not positive or too long for the cancellation is refused.
    /// </summary>
    [TestCaseSource(nameof(kInvalidRunTimeouts))]
    public void Constructor_WithInvalidRunTimeout_Throws(TimeSpan runTimeout)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FwoAgentChatClientFactory(kMiddlewareServerUri, runTimeout));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FwoAgentChatClientFactory(new HttpClient(), kMiddlewareServerUri, runTimeout));
    }

    /// <summary>
    /// Verifies that the own HTTP client leaves the timeout to the run timeout, which defaults to five minutes.
    /// </summary>
    [Test]
    public void Constructor_WithOwnHttpClient_HasNoClientTimeout()
    {
        using FwoAgentChatClientFactory factory = new(kMiddlewareServerUri);

        Assert.That(factory.HttpClient.Timeout, Is.EqualTo(Timeout.InfiniteTimeSpan));
        Assert.That(factory.RunTimeout, Is.EqualTo(FwoAgentChatClientFactory.kDefaultRunTimeout));
        Assert.That(factory.Endpoint, Is.EqualTo(new Uri(kEndpoint)));
    }

    /// <summary>
    /// Verifies that the factory disposes its own HTTP client, also when disposed twice, and creates no chat clients
    /// afterwards.
    /// </summary>
    [Test]
    public void Dispose_WithOwnHttpClient_DisposesHttpClient()
    {
        FwoAgentChatClientFactory factory = new(kMiddlewareServerUri);
        HttpClient ownHttpClient = factory.HttpClient;

        factory.Dispose();
        factory.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(() => ownHttpClient.GetAsync(kMiddlewareServerUri));
        Assert.Throws<ObjectDisposedException>(() => factory.CreateChatClient(ProvideToken));
    }

    /// <summary>
    /// Verifies that the factory does not dispose an HTTP client it was given, and that a chat client needs a token
    /// provider.
    /// </summary>
    [Test]
    public async Task Dispose_WithGivenHttpClient_KeepsHttpClient()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        using HttpClient httpClient = new(handler);
        FwoAgentChatClientFactory factory = new(httpClient, kMiddlewareServerUri);

        Assert.Throws<ArgumentNullException>(() => factory.CreateChatClient(null!));
        factory.Dispose();

        using HttpResponseMessage response = await httpClient.GetAsync(kMiddlewareServerUri);
        Assert.That(handler.DisposeCount, Is.Zero);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Verifies the request of a prompt run: a POST to the AG-UI endpoint with the bearer token, accepting server-sent
    /// events, declaring the protocol version, with the prompt in the forwarded properties, no messages and a new thread.
    /// </summary>
    [Test]
    public async Task PromptRun_PostsPromptInForwardedProperties()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        AgentPromptRequest prompt = new(kPromptName, kPromptArguments);

        await RunAsync(handler, kNoMessages, prompt.ToChatOptions());

        AgUiRecordedRequest request = handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.RequestUri, Is.EqualTo(new Uri(kEndpoint)));
        Assert.That(request.Authorization?.Scheme, Is.EqualTo(kBearerScheme));
        Assert.That(request.Authorization?.Parameter, Is.EqualTo(kAccessToken));
        Assert.That(request.Accept, Is.EqualTo(kEventStreamAccept));
        Assert.That(request.ContentType, Is.EqualTo(kJsonMediaType));
        using JsonDocument body = JsonDocument.Parse(request.Body);
        JsonElement input = body.RootElement;
        Assert.That(input.GetProperty("threadId").GetString(), Is.Not.Empty);
        Assert.That(input.GetProperty("runId").GetString(), Is.Not.Empty);
        Assert.That(input.GetProperty("protocolVersion").GetString(), Is.EqualTo(kProtocolVersion));
        Assert.That(input.GetProperty("messages").GetArrayLength(), Is.Zero);
        JsonElement promptElement = input.GetProperty("forwardedProps").GetProperty("prompt");
        Assert.That(promptElement.GetProperty("name").GetString(), Is.EqualTo(kPromptName));
        Assert.That(promptElement.GetProperty("arguments").GetProperty(kReasonArgument).GetString(), Is.EqualTo(kReason));
        Assert.That(input.TryGetProperty("resume", out _), Is.False);
    }

    /// <summary>
    /// Verifies that the prompt options name the thread, that a blank thread starts a new one, and that each prompt
    /// run gets a new run id.
    /// </summary>
    [Test]
    public async Task PromptRun_WithThreadId_SendsThreadIdAndNewRunIds()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        AgentPromptRequest prompt = new(kPromptName, kPromptArguments);

        await RunAsync(handler, kNoMessages, prompt.ToChatOptions(kThreadId));
        await RunAsync(handler, kNoMessages, prompt.ToChatOptions(kThreadId));

        List<JsonElement> inputs = handler.Requests.Select(request => JsonDocument.Parse(request.Body).RootElement).ToList();
        Assert.That(inputs.Select(input => input.GetProperty("threadId").GetString()), Has.All.EqualTo(kThreadId));
        Assert.That(inputs[0].GetProperty("runId").GetString(), Is.Not.EqualTo(inputs[1].GetProperty("runId").GetString()));
        Assert.That(prompt.ToChatOptions(" ").ConversationId, Is.Null);
    }

    /// <summary>
    /// Verifies the request of a run continuing a conversation: the whole conversation as AG-UI messages, on the thread
    /// of the chat options, without forwarded properties.
    /// </summary>
    [Test]
    public async Task MessageRun_PostsWholeConversation()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        List<ChatMessage> conversation =
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.Assistant, kAnswer),
            new ChatMessage(ChatRole.User, kFollowUp)
        ];

        await RunAsync(handler, conversation, new ChatOptions { ConversationId = kThreadId });

        using JsonDocument body = JsonDocument.Parse(handler.Requests.Single().Body);
        List<JsonElement> messages = body.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.That(messages.Select(message => message.GetProperty("role").GetString()), Is.EqualTo(new List<string> { "user", "assistant", "user" }));
        Assert.That(messages.Select(message => message.GetProperty("content").GetString()), Is.EqualTo(new List<string> { kQuestion, kAnswer, kFollowUp }));
        Assert.That(body.RootElement.GetProperty("threadId").GetString(), Is.EqualTo(kThreadId));
        Assert.That(body.RootElement.TryGetProperty("forwardedProps", out _), Is.False);
    }

    /// <summary>
    /// Verifies the request answering a tool approval request: a resume entry naming the interrupt, the decision and the
    /// tool call with its arguments, and the conversation without the approval messages.
    /// </summary>
    [Test]
    public async Task ApprovalRun_PostsResumeEntryOfAnswer()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        ToolApprovalRequestContent approvalRequest = new(kInterruptId, new FunctionCallContent(kToolCallId, kToolName, kToolArguments));
        List<AIContent> responseContents = [approvalRequest.CreateResponse(true)];
        List<ChatMessage> conversation =
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.User, responseContents)
        ];

        await RunAsync(handler, conversation, new ChatOptions { ConversationId = kThreadId });

        using JsonDocument body = JsonDocument.Parse(handler.Requests.Single().Body);
        Assert.That(body.RootElement.GetProperty("messages").EnumerateArray().Single().GetProperty("content").GetString(), Is.EqualTo(kQuestion));
        JsonElement resume = body.RootElement.GetProperty("resume").EnumerateArray().Single();
        Assert.That(resume.GetProperty("interruptId").GetString(), Is.EqualTo(kInterruptId));
        JsonElement payload = resume.GetProperty("payload");
        Assert.That(payload.GetProperty("approved").GetBoolean(), Is.True);
        JsonElement toolCall = payload.GetProperty("toolCall");
        Assert.That(toolCall.GetProperty("callId").GetString(), Is.EqualTo(kToolCallId));
        Assert.That(toolCall.GetProperty("name").GetString(), Is.EqualTo(kToolName));
        Assert.That(toolCall.GetProperty("arguments").GetProperty(kToolArgumentName).GetString(), Is.EqualTo(kToolArgumentValue));
    }

    /// <summary>
    /// Verifies that the token provider is asked for the token of every run, so that a refreshed token is used.
    /// </summary>
    [Test]
    public async Task Runs_AskTokenProviderForEveryRun()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        List<string> accessTokens = [kAccessToken, kOtherAccessToken];
        Queue<string> tokens = new(accessTokens);
        using FwoAgentChatClientFactory factory = new(new HttpClient(handler), kMiddlewareServerUri);
        IChatClient chatClient = factory.CreateChatClient(_ => Task.FromResult<string?>(tokens.Dequeue()));

        await CollectAsync(chatClient, kQuestionOnly);
        await CollectAsync(chatClient, kQuestionOnly);

        Assert.That(handler.Requests.Select(request => request.Authorization?.Parameter), Is.EqualTo(new List<string> { kAccessToken, kOtherAccessToken }));
    }

    /// <summary>
    /// Verifies that a missing or unusable token fails the run as unauthorized before anything is sent, without naming
    /// the token.
    /// </summary>
    [TestCaseSource(nameof(kUnusableTokens))]
    public void Runs_WithUnusableToken_ThrowUnauthorizedWithoutSending(string? accessToken)
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithEventStream(kFinishedRun);
        using FwoAgentChatClientFactory factory = new(new HttpClient(handler), kMiddlewareServerUri);
        IChatClient chatClient = factory.CreateChatClient(_ => Task.FromResult(accessToken));

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => CollectAsync(chatClient, kQuestionOnly));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.Unauthorized));
        Assert.That(exception?.StatusCode, Is.Null);
        Assert.That(handler.Requests, Is.Empty);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            Assert.That(exception?.Message, Does.Not.Contain(accessToken));
        }
    }

    /// <summary>
    /// Verifies the mapping of the statuses of refused runs to failures, keeping the status and leaving out the token.
    /// </summary>
    [TestCase(HttpStatusCode.Unauthorized, AgentClientFailure.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden, AgentClientFailure.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests, AgentClientFailure.RateLimited)]
    [TestCase(HttpStatusCode.RequestEntityTooLarge, AgentClientFailure.RequestTooLarge)]
    [TestCase(HttpStatusCode.NotFound, AgentClientFailure.Unavailable)]
    [TestCase(HttpStatusCode.BadGateway, AgentClientFailure.Unavailable)]
    [TestCase(HttpStatusCode.ServiceUnavailable, AgentClientFailure.Unavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout, AgentClientFailure.Unavailable)]
    [TestCase(HttpStatusCode.InternalServerError, AgentClientFailure.ServerError)]
    [TestCase(HttpStatusCode.Conflict, AgentClientFailure.ServerError)]
    [TestCase(HttpStatusCode.BadRequest, AgentClientFailure.ServerError)]
    [TestCase(HttpStatusCode.Redirect, AgentClientFailure.ServerError)]
    public void Runs_WithRefusedRun_ThrowFailureOfStatus(HttpStatusCode statusCode, AgentClientFailure expectedFailure)
    {
        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithStatus(statusCode), kQuestionOnly));

        Assert.That(exception?.Failure, Is.EqualTo(expectedFailure));
        Assert.That(exception?.StatusCode, Is.EqualTo(statusCode));
        Assert.That(exception?.RetryAfter, Is.Null);
        Assert.That(exception?.Message, Does.Not.Contain(kAccessToken));
    }

    /// <summary>
    /// Verifies that the time to wait is read from a Retry-After header given as delay.
    /// </summary>
    [Test]
    public void Runs_WithRetryAfterDelay_ThrowRateLimitedWithDelay()
    {
        AgUiFakeHandler handler = AgUiFakeHandler.WithStatus(HttpStatusCode.TooManyRequests,
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(kRetryAfterSeconds)));

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => RunAsync(handler, kQuestionOnly));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.RateLimited));
        Assert.That(exception?.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(kRetryAfterSeconds)));
    }

    /// <summary>
    /// Verifies that the time to wait is computed from a Retry-After header given as date.
    /// </summary>
    [Test]
    public void Runs_WithRetryAfterDate_ThrowRateLimitedWithTimeUntilDate()
    {
        DateTimeOffset retryDate = DateTimeOffset.UtcNow.AddMinutes(kRetryAfterDateMinutes);
        AgUiFakeHandler handler = AgUiFakeHandler.WithStatus(HttpStatusCode.TooManyRequests,
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(retryDate));

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => RunAsync(handler, kQuestionOnly));

        Assert.That(exception?.RetryAfter, Is.GreaterThan(TimeSpan.FromMinutes(kRetryAfterDateMinutes - 1)));
        Assert.That(exception?.RetryAfter, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(kRetryAfterDateMinutes)));
    }

    /// <summary>
    /// Verifies the time to wait of the Retry-After header forms, a date in the past meaning no wait.
    /// </summary>
    [Test]
    public void GetRetryAfter_ReturnsTimeToWait()
    {
        DateTimeOffset now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

        Assert.That(AgUiFailures.GetRetryAfter(null, now), Is.Null);
        Assert.That(AgUiFailures.GetRetryAfter(new RetryConditionHeaderValue(now.AddSeconds(kRetryAfterSeconds)), now),
            Is.EqualTo(TimeSpan.FromSeconds(kRetryAfterSeconds)));
        Assert.That(AgUiFailures.GetRetryAfter(new RetryConditionHeaderValue(now.AddSeconds(-kRetryAfterSeconds)), now), Is.EqualTo(TimeSpan.Zero));
    }

    /// <summary>
    /// Verifies that a harness that cannot be reached is reported as unavailable, with the cause.
    /// </summary>
    [Test]
    public void Runs_WithConnectionFailure_ThrowUnavailable()
    {
        HttpRequestException connectionFailure = new("connection refused");

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.Throwing(connectionFailure), kQuestionOnly));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.Unavailable));
        Assert.That(exception?.InnerException, Is.SameAs(connectionFailure));
        Assert.That(exception?.StatusCode, Is.Null);
    }

    /// <summary>
    /// Verifies that a run exceeding the run timeout, while waiting for the response or while reading it, is reported
    /// as timeout.
    /// </summary>
    [Test]
    public void Runs_ExceedingRunTimeout_ThrowTimeout()
    {
        TimeSpan runTimeout = TimeSpan.FromMilliseconds(kShortTimeoutMilliseconds);
        Pipe unfinishedStream = new();

        AgentClientException? waitingException = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.Hanging(), kQuestionOnly, runTimeout: runTimeout));
        AgentClientException? readingException = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithEventStream(unfinishedStream.Reader.AsStream()), kQuestionOnly, runTimeout: runTimeout));

        Assert.That(waitingException?.Failure, Is.EqualTo(AgentClientFailure.Timeout));
        Assert.That(waitingException?.InnerException, Is.InstanceOf<OperationCanceledException>());
        Assert.That(readingException?.Failure, Is.EqualTo(AgentClientFailure.Timeout));
    }

    /// <summary>
    /// Verifies that a run cancelled by the caller ends with the cancellation, not with a failure of the client.
    /// </summary>
    [Test]
    public void Runs_CancelledByCaller_ThrowOperationCanceled()
    {
        using FwoAgentChatClientFactory factory = new(new HttpClient(AgUiFakeHandler.Hanging()), kMiddlewareServerUri);
        IChatClient chatClient = factory.CreateChatClient(ProvideToken);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(kShortTimeoutMilliseconds));

        Assert.CatchAsync<OperationCanceledException>(() => chatClient.GetResponseAsync(kQuestionOnly, null, cancellation.Token));
    }

    /// <summary>
    /// Runs the conversation with a chat client of a factory using the given handler, returning the response.
    /// </summary>
    private static async Task<ChatResponse> RunAsync(AgUiFakeHandler handler, IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        TimeSpan? runTimeout = null)
    {
        using FwoAgentChatClientFactory factory = new(new HttpClient(handler), kMiddlewareServerUri, runTimeout);
        IChatClient chatClient = factory.CreateChatClient(ProvideToken);
        return await chatClient.GetResponseAsync(messages, options);
    }

    /// <summary>
    /// Reads all updates of a run.
    /// </summary>
    private static async Task<List<ChatResponseUpdate>> CollectAsync(IChatClient chatClient, IEnumerable<ChatMessage> messages)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }
        return updates;
    }

    /// <summary>
    /// Returns the access token of the tests.
    /// </summary>
    private static Task<string?> ProvideToken(CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(kAccessToken);
    }
}
