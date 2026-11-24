using FWO.Ai.Client;
using FWO.Ai.Client.ReasonChecks;
using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the chat client of the UI (FWO.Ai.Client) against the AG-UI endpoint of the stateless harness server on a test
/// server, with a scripted chat client instead of an AI provider and the MCP server pipeline on another test server:
/// prompts, conversations sent again with every run, tool approval through interrupts and resume entries, run errors
/// and refused runs.
/// </summary>
[TestFixture]
internal class ClientHarnessTest
{
    private const string kThreadId = "thread-client";
    private const string kQuestion = "Which roles do I have?";
    private const string kNextQuestion = "And now?";
    private const string kAnswer = "Verdict: GENUINE\nCriteria:\nPurpose: met";
    private const string kNextAnswer = "Still a reporter.";
    private const string kReason = "Der Applikationsserver liest Kundendaten für die Auftragsprüfung aus der Datenbank.";
    private const string kConnectionName = "Kundendaten lesen";
    private const string kGerman = "German";
    private const string kGivenLanguageInstruction = "in the language \"German\"";
    private const string kInterfaceHint = "interface the app offers to other apps";
    private const string kCallId = "call-store";
    private const string kValue = "value-approved";
    private const string kProviderFailure = "provider unavailable";
    private const string kInvalidToken = "not-a-jwt";
    private const char kLongContextCharacter = 'x';
    private const int kSingleRunPerMinute = 1;
    private const int kContextOverLimit = ReasonCheckLimits.kMaxContextLength + 500;

    private static readonly Uri kTestServerUri = new("http://localhost/");
    private static readonly List<string> kApprovedValues = [kValue];
    private static readonly List<string> kNoValues = [];
    private static readonly List<string> kCallIds = [kCallId];
    private static readonly List<string> kFirstAndSecondTurn = [kQuestion, kAnswer, kNextQuestion];
    private static readonly List<string> kBothTurns = [kQuestion, kAnswer, kNextQuestion, kNextAnswer];
    private static readonly List<string> kQuestionOnly = [kQuestion];
    private static readonly List<ChatRole> kPromptAndAnswerRoles = [ChatRole.User, ChatRole.Assistant];
    private static readonly List<ChatRole> kToolTurnRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant];
    private static readonly List<ChatMessage> kNoMessages = [];

    /// <summary>
    /// Verifies that a connection reason check runs the MCP prompt with the reason and the language through the harness,
    /// streams the answer, names the thread of the run, and ends with the conversation: the expanded prompt and the answer.
    /// </summary>
    [Test]
    public async Task RunPrompt_WithConnectionReasonCheck_StreamsAnswerAndReturnsConversation()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Modeller));
        ConnectionReasonCheck check = new() { Reason = kReason, Kind = ConnectionReasonKind.Interface, Name = kConnectionName, Language = kGerman };

        ClientRun run = await ClientRun.RunAsync(client, kNoMessages, check.ToPromptRequest().ToChatOptions());

        Assert.That(run.Text, Is.EqualTo(kAnswer));
        Assert.That(run.ThreadId, Is.Not.Null.And.Not.Empty);
        Assert.That(run.Errors, Is.Empty);
        Assert.That(ReasonVerdictParser.Parse(run.Text), Is.EqualTo(ReasonVerdict.Genuine));
        string sentPrompt = chatClient.ReceivedMessages.Single().Single().Text;
        Assert.That(sentPrompt, Does.Contain(kReason));
        Assert.That(sentPrompt, Does.Contain(kConnectionName));
        Assert.That(sentPrompt, Does.Contain(kInterfaceHint));
        Assert.That(sentPrompt, Does.Contain(kGivenLanguageInstruction));
        Assert.That(run.Conversation?.Select(message => message.Role), Is.EqualTo(kPromptAndAnswerRoles));
        Assert.That(run.Conversation?[0].Text, Is.EqualTo(sentPrompt));
        Assert.That(run.Conversation?[1].Text, Is.EqualTo(kAnswer));
    }

    /// <summary>
    /// Verifies that the MCP server accepts a reason check whose arguments are at the limits of the client, so that the
    /// limits of the client and the server are in step.
    /// </summary>
    [Test]
    public async Task RunPrompt_WithArgumentsAtClientLimits_IsAccepted()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Modeller));
        string longContext = new(kLongContextCharacter, kContextOverLimit);
        ConnectionReasonCheck check = new()
        {
            Reason = new string('r', ReasonCheckLimits.kMaxReasonLength),
            Name = longContext,
            App = longContext,
            Source = longContext,
            Destination = longContext,
            Service = longContext,
            Language = new string('l', ReasonCheckLimits.kMaxLanguageLength)
        };

        ChatResponse response = await client.GetResponseAsync(kNoMessages, check.ToPromptRequest().ToChatOptions());

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        Assert.That(AgentConversation.TryGetSnapshot(response, out List<ChatMessage>? conversation), Is.True);
        Assert.That(conversation, Has.Count.EqualTo(kPromptAndAnswerRoles.Count));
        Assert.That(chatClient.ReceivedMessages.Single().Single().Text, Does.Contain(ConnectionReasonCheck.kCutMarker));
    }

    /// <summary>
    /// Verifies that a run sending the conversation of the previous run followed by a new question continues that
    /// conversation, although the harness keeps none, and ends with the conversation of both turns.
    /// </summary>
    [Test]
    public async Task Run_WithConversationOfPreviousRun_ContinuesConversation()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        List<ChatMessage> firstMessages = [new ChatMessage(ChatRole.User, kQuestion)];

        ClientRun first = await ClientRun.RunAsync(client, firstMessages, new ChatOptions());
        List<ChatMessage> secondMessages = [.. first.Conversation!, new ChatMessage(ChatRole.User, kNextQuestion)];
        ClientRun second = await ClientRun.RunAsync(client, secondMessages, new ChatOptions { ConversationId = first.ThreadId });

        Assert.That(first.Text, Is.EqualTo(kAnswer));
        Assert.That(second.Text, Is.EqualTo(kNextAnswer));
        Assert.That(second.ThreadId, Is.EqualTo(first.ThreadId));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kFirstAndSecondTurn));
        Assert.That(HarnessTestSetup.GetTexts(second.Conversation!), Is.EqualTo(kBothTurns));
    }

    /// <summary>
    /// Verifies the approval of a tool that is not read-only: the run is interrupted with the approval request naming
    /// the tool call, and resuming it with the conversation of the interrupted run and the answer runs the tool only if
    /// approved. The conversation of the resumed run holds the tool call and its result, and a provider accepts it as
    /// history of the next run.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task Resume_AfterInterrupt_RunsToolOnlyIfApproved(bool approved)
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication mcpApp = await McpTestServer.StartWith(mcpServer => McpTestWriteTools.Register(mcpServer, toolCalls));
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        List<ChatMessage> questionMessages = [new ChatMessage(ChatRole.User, kQuestion)];

        ClientRun interrupted = await ClientRun.RunAsync(client, questionMessages, new ChatOptions { ConversationId = kThreadId });

        ToolApprovalRequestContent request = interrupted.ApprovalRequests.Single();
        FunctionCallContent? requestedCall = request.ToolCall as FunctionCallContent;
        Assert.That(request.RequestId, Is.Not.Empty);
        Assert.That(requestedCall?.CallId, Is.EqualTo(kCallId));
        Assert.That(requestedCall?.Name, Is.EqualTo(McpTestWriteTools.kStoreValueTool));
        Assert.That(GetStringArgument(requestedCall, McpTestWriteTools.kValueArgument), Is.EqualTo(kValue));
        Assert.That(HarnessTestSetup.GetTexts(interrupted.Conversation!), Is.EqualTo(kQuestionOnly));
        Assert.That(toolCalls.StoredValues, Is.Empty);

        List<AIContent> approvalAnswer = [request.CreateResponse(approved)];
        List<ChatMessage> resumeMessages = [.. interrupted.Conversation!, new ChatMessage(ChatRole.User, approvalAnswer)];
        ClientRun resumed = await ClientRun.RunAsync(client, resumeMessages, new ChatOptions { ConversationId = kThreadId });

        Assert.That(resumed.Text, Is.EqualTo(kAnswer));
        Assert.That(toolCalls.StoredValues, Is.EqualTo(approved ? kApprovedValues : kNoValues));
        Assert.That(resumed.Conversation?.Select(message => message.Role), Is.EqualTo(kToolTurnRoles));
        Assert.That(HarnessTestSetup.GetCallIds(resumed.Conversation!), Is.EqualTo(kCallIds));
        Assert.That(HarnessTestSetup.GetResultCallIds(resumed.Conversation!), Is.EqualTo(kCallIds));

        List<ChatMessage> nextMessages = [.. resumed.Conversation!, new ChatMessage(ChatRole.User, kNextQuestion)];
        ClientRun next = await ClientRun.RunAsync(client, nextMessages, new ChatOptions { ConversationId = kThreadId });

        Assert.That(next.Text, Is.EqualTo(kNextAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run failing in the harness ends with an error, without the details of the failure and without
    /// a conversation, so that the client keeps the conversation it sent.
    /// </summary>
    [Test]
    public async Task Run_WhenProviderFails_ReturnsErrorWithoutConversation()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient()
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kProviderFailure)));
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        List<ChatMessage> messages = [new ChatMessage(ChatRole.User, kQuestion)];

        ClientRun run = await ClientRun.RunAsync(client, messages, new ChatOptions());

        ErrorContent error = run.Errors.Single();
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Not.Contain(kProviderFailure));
        Assert.That(run.Conversation, Is.Null);
    }

    /// <summary>
    /// Verifies that a run with an invalid token is refused as unauthorized, without the token in the message.
    /// </summary>
    [Test]
    public async Task Run_WithInvalidToken_ThrowsUnauthorized()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, kInvalidToken);

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => client.GetResponseAsync(kQuestion));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.Unauthorized));
        Assert.That(exception?.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(exception?.Message, Does.Not.Contain(kInvalidToken));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run of a user without an FWO user role is refused as forbidden.
    /// </summary>
    [Test]
    public async Task Run_WithAnonymousRole_ThrowsForbidden()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Anonymous));

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => client.GetResponseAsync(kQuestion));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.Forbidden));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run beyond the runs per minute of the user is refused as rate limited, with the time to wait.
    /// </summary>
    [Test]
    public async Task Run_BeyondRunsPerMinute_ThrowsRateLimited()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        HarnessSettings settings = new() { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint, RunsPerMinute = kSingleRunPerMinute };
        await using WebApplication app = await HarnessTestServer.Start(mcpApp, chatClient, settings);
        using HttpClient httpClient = app.GetTestClient();
        using FwoAgentChatClientFactory factory = new(httpClient, kTestServerUri);
        using IChatClient client = CreateClient(factory, McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        await client.GetResponseAsync(kQuestion);

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(() => client.GetResponseAsync(kNextQuestion));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.RateLimited));
        Assert.That(exception?.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(HarnessRateLimiting.kRetryAfterSeconds)));
    }

    /// <summary>
    /// Creates a chat client sending its runs to the harness on the test server with the given token.
    /// </summary>
    private static IChatClient CreateClient(FwoAgentChatClientFactory factory, string accessToken)
    {
        return factory.CreateChatClient(_ => Task.FromResult<string?>(accessToken));
    }

    /// <summary>
    /// Creates the arguments of a call of the test tool storing the given value.
    /// </summary>
    private static Dictionary<string, object?> CreateArguments(string value)
    {
        return new Dictionary<string, object?> { [McpTestWriteTools.kValueArgument] = value };
    }

    /// <summary>
    /// Returns the value of a string argument of a tool call, which the AG-UI chat client reads as JSON.
    /// </summary>
    private static string? GetStringArgument(FunctionCallContent? call, string name)
    {
        object? value = null;
        call?.Arguments?.TryGetValue(name, out value);
        return value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value?.ToString();
    }

    /// <summary>
    /// What a streamed run returned: the text of the answer, the thread, the tool approval requests, the errors and the
    /// conversation the harness sent at the end of the run.
    /// </summary>
    private sealed class ClientRun
    {
        private readonly StringBuilder text = new();

        /// <summary>Text of the answer of the run.</summary>
        internal string Text => text.ToString();

        /// <summary>Thread of the run, named by RUN_STARTED.</summary>
        internal string? ThreadId { get; private set; }

        /// <summary>Conversation the harness sent at the end of the run; null if it sent none.</summary>
        internal List<ChatMessage>? Conversation { get; private set; }

        /// <summary>Tool approval requests that interrupted the run.</summary>
        internal List<ToolApprovalRequestContent> ApprovalRequests { get; } = [];

        /// <summary>Errors the run ended with.</summary>
        internal List<ErrorContent> Errors { get; } = [];

        /// <summary>
        /// Sends a run with the given messages and options and collects its updates.
        /// </summary>
        internal static async Task<ClientRun> RunAsync(IChatClient client, IEnumerable<ChatMessage> messages, ChatOptions options)
        {
            ClientRun run = new();
            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(messages, options))
            {
                run.Apply(update);
            }
            return run;
        }

        /// <summary>
        /// Collects an update of the run.
        /// </summary>
        private void Apply(ChatResponseUpdate update)
        {
            if (AgentConversation.TryGetThreadId(update, out string? threadId))
            {
                ThreadId = threadId;
            }
            if (AgentConversation.TryGetSnapshot(update, out List<ChatMessage>? conversation))
            {
                Conversation = conversation;
            }
            if (update.Role == ChatRole.Assistant || update.Role == null)
            {
                text.Append(update.Text);
            }
            ApprovalRequests.AddRange(update.Contents.OfType<ToolApprovalRequestContent>());
            Errors.AddRange(update.Contents.OfType<ErrorContent>());
        }
    }
}
