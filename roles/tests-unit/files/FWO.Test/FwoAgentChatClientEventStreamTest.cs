using FWO.Ai.Client;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.IO.Pipelines;
using System.Text;

namespace FWO.Test;

/// <summary>
/// Tests how the chat clients of the FWO agent read the server-sent events of a run with a fake handler: the answer
/// text, the thread, interrupts with the tool calls they ask about, run errors, the snapshot of the conversation, events
/// the client does not know, the server-sent event format, broken streams, protocol violations and the size limit.
/// </summary>
[TestFixture]
[Parallelizable]
internal class FwoAgentChatClientEventStreamTest
{
    private const string kAccessToken = "header.payload.signature";
    private const string kQuestion = "Check the reason.";
    private const string kServerThreadId = "thread-server";
    private const string kFirstDelta = "Verdict: ";
    private const string kSecondDelta = "GENUINE";
    private const string kOtherMessageDelta = "Criteria:";
    private const string kInterruptId = "interrupt-1";
    private const string kToolCallId = "call-1";
    private const string kToolName = "store_value";
    private const string kToolArgumentName = "value";
    private const string kToolArgumentValue = "x";
    private const string kErrorMessage = "The run failed.";
    private const string kErrorCode = "agent_error";
    private const string kSnapshotQuestion = "Q";
    private const string kSnapshotAnswer = "A";
    private const string kRunStarted = """{"type":"RUN_STARTED","threadId":"thread-server","runId":"run-server"}""";
    private const string kTextStart = """{"type":"TEXT_MESSAGE_START","messageId":"message-1","role":"assistant"}""";
    private const string kFirstContent = """{"type":"TEXT_MESSAGE_CONTENT","messageId":"message-1","delta":"Verdict: "}""";
    private const string kSecondContent = """{"type":"TEXT_MESSAGE_CONTENT","messageId":"message-1","delta":"GENUINE"}""";
    private const string kTextEnd = """{"type":"TEXT_MESSAGE_END","messageId":"message-1"}""";
    private const string kOtherTextStart = """{"type":"TEXT_MESSAGE_START","messageId":"message-2","role":"assistant"}""";
    private const string kOtherMessageContent = """{"type":"TEXT_MESSAGE_CONTENT","messageId":"message-2","delta":"Criteria:"}""";
    private const string kOtherTextEnd = """{"type":"TEXT_MESSAGE_END","messageId":"message-2"}""";
    private const string kRunFinished = """{"type":"RUN_FINISHED","threadId":"thread-server","runId":"run-server"}""";
    private const string kToolCallStart = """{"type":"TOOL_CALL_START","toolCallId":"call-1","toolCallName":"store_value"}""";
    private const string kToolCallFirstArguments = """{"type":"TOOL_CALL_ARGS","toolCallId":"call-1","delta":"{\"value\":"}""";
    private const string kToolCallSecondArguments = """{"type":"TOOL_CALL_ARGS","toolCallId":"call-1","delta":"\"x\"}"}""";
    private const string kToolCallEnd = """{"type":"TOOL_CALL_END","toolCallId":"call-1"}""";
    private const string kInterrupted = """{"type":"RUN_FINISHED","threadId":"thread-server","runId":"run-server","outcome":{"type":"interrupt","interrupts":[{"id":"interrupt-1","reason":"tool_call","toolCallId":"call-1"}]}}""";
    private const string kRunError = """{"type":"RUN_ERROR","message":"The run failed.","code":"agent_error"}""";
    private const string kSnapshot = """{"type":"MESSAGES_SNAPSHOT","messages":[{"id":"m-1","role":"user","content":"Q"},{"id":"m-2","role":"assistant","content":"A"}]}""";
    private const string kUnknownEvent = """{"type":"FUTURE_EVENT","value":1}""";
    private const string kMalformedEvent = """{"type":"TEXT_MESSAGE_CONTENT",""";
    private const string kNullEvent = "null";
    private const string kComment = ": keep-alive\n\n";
    private const int kSmallResponseLimit = 100;
    private const int kSingleByte = 1;
    private const int kThreeBytes = 3;
    private const int kStreamWaitSeconds = 10;

    private static readonly Uri kMiddlewareServerUri = new("https://fwo.example:8888/");
    private static readonly List<ChatMessage> kQuestionOnly = [new ChatMessage(ChatRole.User, kQuestion)];
    private static readonly List<string> kAnswerDeltas = [kFirstDelta, kSecondDelta];
    private static readonly List<ChatRole> kSnapshotRoles = [ChatRole.User, ChatRole.Assistant];
    private static readonly List<string> kSnapshotTexts = [kSnapshotQuestion, kSnapshotAnswer];

    /// <summary>
    /// Verifies that a completed run returns the answer text piece by piece, and the thread the harness names.
    /// </summary>
    [Test]
    public async Task Run_Completed_ReturnsAnswerAndThread()
    {
        string eventStream = CreateEventStream(kRunStarted, kTextStart, kFirstContent, kSecondContent, kTextEnd, kRunFinished);

        List<ChatResponseUpdate> updates = await CollectAsync(AgUiFakeHandler.WithEventStream(eventStream));

        Assert.That(updates.Select(update => update.Text).Where(text => text.Length > 0), Is.EqualTo(kAnswerDeltas));
        Assert.That(updates.Select(GetThreadId).OfType<string>().Single(), Is.EqualTo(kServerThreadId));
        Assert.That(updates.SelectMany(update => update.Contents).OfType<ErrorContent>(), Is.Empty);
        Assert.That(updates.SelectMany(update => update.Contents).OfType<ToolApprovalRequestContent>(), Is.Empty);
    }

    /// <summary>
    /// Verifies that events split at any byte across the reads are put together.
    /// </summary>
    [TestCase(kSingleByte)]
    [TestCase(kThreeBytes)]
    public async Task Run_WithEventsSplitAcrossReads_ReturnsAnswer(int chunkSize)
    {
        string eventStream = CreateEventStream(kRunStarted, kTextStart, kFirstContent, kSecondContent, kTextEnd, kRunFinished);

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(AgUiChunkedStream.Split(eventStream, chunkSize)));

        Assert.That(response.Text, Is.EqualTo(kFirstDelta + kSecondDelta));
    }

    /// <summary>
    /// Verifies the server-sent event format: comments, event types, ids and retry fields are skipped, CRLF line ends
    /// are accepted, and the data lines of an event are joined.
    /// </summary>
    [Test]
    public async Task Run_WithFullEventFormat_ReadsDataOfEvents()
    {
        string eventStream = kComment
            + "event: message\r\nid: 1\r\nretry: 1000\r\ndata: " + kRunStarted + "\r\n\r\n"
            + "data: " + kTextStart + "\n\n"
            + "data: {\"type\":\"TEXT_MESSAGE_CONTENT\",\n" + "data: \"messageId\":\"message-1\",\"delta\":\"Verdict: \"}\n\n"
            + "data:" + kSecondContent + "\n\n"
            + kComment
            + CreateEventStream(kTextEnd, kRunFinished);

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        Assert.That(response.Text, Is.EqualTo(kFirstDelta + kSecondDelta));
    }

    /// <summary>
    /// Verifies that the pieces of text are passed on while the response is still streaming.
    /// </summary>
    [Test]
    public async Task Run_WhileResponseStreams_PassesTextOnAsItArrives()
    {
        Pipe response = new();
        using FwoAgentChatClientFactory factory = new(new HttpClient(AgUiFakeHandler.WithEventStream(response.Reader.AsStream())), kMiddlewareServerUri);
        IChatClient chatClient = factory.CreateChatClient(ProvideToken);
        await using IAsyncEnumerator<ChatResponseUpdate> updates = chatClient.GetStreamingResponseAsync(kQuestionOnly).GetAsyncEnumerator();

        await response.Writer.WriteAsync(Encoding.UTF8.GetBytes(CreateEventStream(kRunStarted, kTextStart, kFirstContent)));
        string firstText = await ReadFirstTextAsync(updates).WaitAsync(TimeSpan.FromSeconds(kStreamWaitSeconds));
        await response.Writer.WriteAsync(Encoding.UTF8.GetBytes(CreateEventStream(kTextEnd, kRunFinished)));
        await response.Writer.CompleteAsync();
        while (await updates.MoveNextAsync())
        {
            // read the rest of the run
        }

        Assert.That(firstText, Is.EqualTo(kFirstDelta));
    }

    /// <summary>
    /// Verifies that the texts of different messages of a run come as updates of their own messages.
    /// </summary>
    [Test]
    public async Task Run_WithSeveralMessages_KeepsTheirTextsApart()
    {
        string eventStream = CreateEventStream(kRunStarted, kTextStart, kFirstContent, kSecondContent, kTextEnd,
            kOtherTextStart, kOtherMessageContent, kOtherTextEnd, kRunFinished);

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        List<string> texts = response.Messages.Select(message => message.Text).Where(text => text.Length > 0).ToList();
        Assert.That(texts, Is.EqualTo(new List<string> { kFirstDelta + kSecondDelta, kOtherMessageDelta }));
    }

    /// <summary>
    /// Verifies that events of types the client does not know are ignored, so that newer servers can send them.
    /// </summary>
    [Test]
    public async Task Run_WithUnknownEvents_IgnoresThem()
    {
        string eventStream = CreateEventStream(kRunStarted, kUnknownEvent, kTextStart, kFirstContent, kSecondContent, kTextEnd, kRunFinished);

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        Assert.That(response.Text, Is.EqualTo(kFirstDelta + kSecondDelta));
    }

    /// <summary>
    /// Verifies that an interrupted run returns a tool approval request per interrupt, describing the tool call
    /// streamed before.
    /// </summary>
    [Test]
    public async Task Run_WithInterrupt_ReturnsApprovalRequestOfToolCall()
    {
        string eventStream = CreateEventStream(kRunStarted, kToolCallStart, kToolCallFirstArguments, kToolCallSecondArguments, kToolCallEnd, kInterrupted);

        List<ChatResponseUpdate> updates = await CollectAsync(AgUiFakeHandler.WithEventStream(eventStream));

        ToolApprovalRequestContent approvalRequest = updates.SelectMany(update => update.Contents).OfType<ToolApprovalRequestContent>().Single();
        FunctionCallContent toolCall = (FunctionCallContent)approvalRequest.ToolCall;
        Assert.That(approvalRequest.RequestId, Is.EqualTo(kInterruptId));
        Assert.That(toolCall.CallId, Is.EqualTo(kToolCallId));
        Assert.That(toolCall.Name, Is.EqualTo(kToolName));
        Assert.That(toolCall.Arguments?[kToolArgumentName]?.ToString(), Is.EqualTo(kToolArgumentValue));
    }

    /// <summary>
    /// Verifies that a run error returns the message and code of the harness, after the text so far.
    /// </summary>
    [Test]
    public async Task Run_WithRunError_ReturnsErrorContent()
    {
        List<ChatResponseUpdate> updates = await CollectAsync(AgUiFakeHandler.WithEventStream(CreateEventStream(kRunStarted, kTextStart, kFirstContent, kRunError)));

        ErrorContent error = updates.SelectMany(update => update.Contents).OfType<ErrorContent>().Single();
        Assert.That(error.Message, Is.EqualTo(kErrorMessage));
        Assert.That(error.ErrorCode, Is.EqualTo(kErrorCode));
        Assert.That(string.Concat(updates.Select(update => update.Text)), Is.EqualTo(kFirstDelta));
    }

    /// <summary>
    /// Verifies that the snapshot of the conversation the harness sends is returned with the update and with the
    /// whole response.
    /// </summary>
    [Test]
    public async Task Run_WithSnapshot_ReturnsConversation()
    {
        string eventStream = CreateEventStream(kRunStarted, kTextStart, kFirstContent, kTextEnd, kSnapshot, kRunFinished);

        List<ChatResponseUpdate> updates = await CollectAsync(AgUiFakeHandler.WithEventStream(eventStream));
        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        List<List<ChatMessage>> streamedSnapshots = updates.Select(GetSnapshot).OfType<List<ChatMessage>>().ToList();
        Assert.That(streamedSnapshots, Has.Count.EqualTo(1));
        Assert.That(AgentConversation.TryGetSnapshot(response, out List<ChatMessage>? responseSnapshot), Is.True);
        foreach (List<ChatMessage> snapshot in streamedSnapshots.Append(responseSnapshot!))
        {
            Assert.That(snapshot.Select(message => message.Role), Is.EqualTo(kSnapshotRoles));
            Assert.That(snapshot.Select(message => message.Text), Is.EqualTo(kSnapshotTexts));
        }
    }

    /// <summary>
    /// Verifies that a run without snapshot returns none, and that the helpers refuse null.
    /// </summary>
    [Test]
    public async Task Run_WithoutSnapshot_ReturnsNone()
    {
        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(CreateEventStream(kRunStarted, kRunFinished)));

        Assert.That(AgentConversation.TryGetSnapshot(response, out List<ChatMessage>? snapshot), Is.False);
        Assert.That(snapshot, Is.Null);
        Assert.That(AgentConversation.TryGetSnapshot(new ChatResponseUpdate(), out _), Is.False);
        Assert.That(AgentConversation.TryGetThreadId(new ChatResponseUpdate(), out string? threadId), Is.False);
        Assert.That(threadId, Is.Null);
        Assert.Throws<ArgumentNullException>(() => AgentConversation.TryGetSnapshot((ChatResponse)null!, out _));
        Assert.Throws<ArgumentNullException>(() => AgentConversation.TryGetSnapshot((ChatResponseUpdate)null!, out _));
        Assert.Throws<ArgumentNullException>(() => AgentConversation.TryGetThreadId(null!, out _));
    }

    /// <summary>
    /// Verifies that the events after the end of the run are not passed on, so that they cannot change the result.
    /// </summary>
    [Test]
    public async Task Run_WithEventsAfterEndOfRun_IgnoresThem()
    {
        string eventStream = CreateEventStream(kRunStarted, kTextStart, kFirstContent, kTextEnd, kRunFinished, kOtherTextStart, kOtherMessageContent, kMalformedEvent);

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        Assert.That(response.Text, Is.EqualTo(kFirstDelta));
    }

    /// <summary>
    /// Verifies that a connection breaking off after the end of the run does not change its result.
    /// </summary>
    [Test]
    public async Task Run_WithConnectionBrokenAfterEndOfRun_ReturnsResult()
    {
        AgUiChunkedStream eventStream = AgUiChunkedStream.Split(CreateEventStream(kRunStarted, kTextStart, kFirstContent, kTextEnd, kRunFinished),
            kThreeBytes, new IOException("reset"));

        ChatResponse response = await RunAsync(AgUiFakeHandler.WithEventStream(eventStream));

        Assert.That(response.Text, Is.EqualTo(kFirstDelta));
    }

    /// <summary>
    /// Verifies that data that is no AG-UI event is a protocol error.
    /// </summary>
    [TestCase(kMalformedEvent)]
    [TestCase(kNullEvent)]
    public void Run_WithInvalidEvent_ThrowsProtocolError(string invalidEvent)
    {
        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithEventStream(CreateEventStream(kRunStarted, invalidEvent, kRunFinished))));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
    }

    /// <summary>
    /// Verifies that a stream breaking the event lifecycle, e.g. not starting with RUN_STARTED, is a protocol error.
    /// </summary>
    [Test]
    public void Run_BreakingEventLifecycle_ThrowsProtocolError()
    {
        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithEventStream(CreateEventStream(kTextStart, kFirstContent, kTextEnd, kRunFinished))));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
        Assert.That(exception?.InnerException, Is.InstanceOf<InvalidOperationException>());
    }

    /// <summary>
    /// Verifies that a stream ending or breaking off before the end of the run is a protocol error.
    /// </summary>
    [Test]
    public void Run_WithStreamEndingBeforeEndOfRun_ThrowsProtocolError()
    {
        IOException connectionReset = new("reset");
        string unfinishedRun = CreateEventStream(kRunStarted, kTextStart, kFirstContent);

        AgentClientException? endedException = Assert.ThrowsAsync<AgentClientException>(() => RunAsync(AgUiFakeHandler.WithEventStream(unfinishedRun)));
        AgentClientException? brokenException = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithEventStream(AgUiChunkedStream.Split(unfinishedRun, kThreeBytes, connectionReset))));
        AgentClientException? emptyException = Assert.ThrowsAsync<AgentClientException>(() => RunAsync(AgUiFakeHandler.WithEventStream("")));

        Assert.That(endedException?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
        Assert.That(brokenException?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
        Assert.That(brokenException?.InnerException, Is.SameAs(connectionReset));
        Assert.That(emptyException?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
    }

    /// <summary>
    /// Verifies that a response larger than the limit is a protocol error, also if a single event never ends.
    /// </summary>
    [Test]
    public void Run_WithResponseOverLimit_ThrowsProtocolError()
    {
        string endlessEvent = "data: " + new string('x', kSmallResponseLimit * 2);

        AgentClientException? exception = Assert.ThrowsAsync<AgentClientException>(
            () => RunAsync(AgUiFakeHandler.WithEventStream(endlessEvent), kSmallResponseLimit));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
    }

    /// <summary>
    /// Runs the question with a chat client using the given handler and response limit, returning the whole response.
    /// </summary>
    private static async Task<ChatResponse> RunAsync(AgUiFakeHandler handler, long maxResponseBytes = FwoAgentChatClientFactory.kMaxResponseBytes)
    {
        using FwoAgentChatClientFactory factory = new(new HttpClient(handler), kMiddlewareServerUri) { MaxResponseBytes = maxResponseBytes };
        return await factory.CreateChatClient(ProvideToken).GetResponseAsync(kQuestionOnly);
    }

    /// <summary>
    /// Runs the question with a chat client using the given handler, returning the updates.
    /// </summary>
    private static async Task<List<ChatResponseUpdate>> CollectAsync(AgUiFakeHandler handler)
    {
        using FwoAgentChatClientFactory factory = new(new HttpClient(handler), kMiddlewareServerUri);
        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in factory.CreateChatClient(ProvideToken).GetStreamingResponseAsync(kQuestionOnly))
        {
            updates.Add(update);
        }
        return updates;
    }

    /// <summary>
    /// Reads the updates until the first one with text, and returns its text.
    /// </summary>
    private static async Task<string> ReadFirstTextAsync(IAsyncEnumerator<ChatResponseUpdate> updates)
    {
        while (await updates.MoveNextAsync())
        {
            if (updates.Current.Text.Length > 0)
            {
                return updates.Current.Text;
            }
        }
        return "";
    }

    /// <summary>
    /// Returns the thread an update names, or null.
    /// </summary>
    private static string? GetThreadId(ChatResponseUpdate update)
    {
        return AgentConversation.TryGetThreadId(update, out string? threadId) ? threadId : null;
    }

    /// <summary>
    /// Returns the conversation of a snapshot update, or null.
    /// </summary>
    private static List<ChatMessage>? GetSnapshot(ChatResponseUpdate update)
    {
        return AgentConversation.TryGetSnapshot(update, out List<ChatMessage>? snapshot) ? snapshot : null;
    }

    /// <summary>
    /// Returns the access token of the tests.
    /// </summary>
    private static Task<string?> ProvideToken(CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(kAccessToken);
    }

    /// <summary>
    /// Creates the server-sent events of the given AG-UI events, one data line each.
    /// </summary>
    private static string CreateEventStream(params string[] aguiEvents)
    {
        return string.Concat(aguiEvents.Select(aguiEvent => $"data: {aguiEvent}\n\n"));
    }
}
