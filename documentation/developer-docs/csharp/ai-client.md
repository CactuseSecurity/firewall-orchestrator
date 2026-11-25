# AI client (FWO.Ai.Client)

`roles/lib/files/FWO.Ai.Client` is the client of the AG-UI endpoint of the [AI harness](ai-harness.md). The FWO UI
uses it to run prompts of the [AI MCP server](ai-mcp.md) as the logged-in user, e.g. the check of the functional reason
of a modelled connection. It is a shared library and provides an `IChatClient` of Microsoft.Extensions.AI.

| Feature | Provided by |
|---|---|
| Endpoint | `<middleware server uri>agui` (`FwoAgentChatClientFactory.kAgUiPath`), published by the Apache of the middleware |
| Chat clients | `IFwoAgentChatClientFactory` (`FwoAgentChatClientFactory`): `CreateChatClient(accessTokenProvider)` returns an `IChatClient` |
| Prompts | `AgentPromptRequest.ToChatOptions`: an MCP prompt as the user's turn |
| Conversation | `AgentConversation`: the snapshot of the conversation and the thread of a run |
| Failures | `AgentClientException` with `AgentClientFailure` |
| Reason check | `ConnectionReasonCheck` and `ReasonVerdictParser` (`FWO.Ai.Client.ReasonChecks`) |

```csharp
using FwoAgentChatClientFactory factory = new(new Uri("https://fwo.example:8888/"));
using IChatClient client = factory.CreateChatClient(cancellationToken => GetAccessTokenOfUserAsync(cancellationToken));
ConnectionReasonCheck check = new() { Reason = reason, Kind = ConnectionReasonKind.Interface, Name = name, Language = "German" };
StringBuilder answer = new();
await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync([], check.ToPromptRequest().ToChatOptions(), cancellationToken))
{
    answer.Append(update.Text);
    ShowPartialAnswer(answer.ToString());
}
ReasonVerdict verdict = ReasonVerdictParser.Parse(answer.ToString());
```

The UI registers one `FwoAgentChatClientFactory` as singleton; a chat client is cheap and is created per use with the
token provider of the session.

## Runs

Each call of `GetResponseAsync` or `GetStreamingResponseAsync` posts one AG-UI run (`RunAgentInput`) to the endpoint,
with `Authorization: Bearer <JWT of the user>` and `Accept: text/event-stream`, and returns the events of the run as
updates as they arrive (see [AG-UI endpoint](ai-harness.md#ag-ui-endpoint)). The client keeps no token: it calls the
token provider at the start of every run and sends the token it returns, so that a token the UI renewed in the meantime is used.

- The messages of the call are the conversation, see [Conversation](#conversation).
- `ChatOptions.ConversationId` is the AG-UI thread id; a new thread id if none is given. The harness keeps no state of
  a thread, the thread only groups the runs of a conversation.
- `AgentPromptRequest.ToChatOptions(threadId)` sends an MCP prompt as the user's turn: the prompt in
  `forwardedProps.prompt` (`{ "name": ..., "arguments": { ... } }`, see [MCP prompts](ai-harness.md#mcp-prompts)),
  with the conversation so far and no new user message. The options are for one run only.
- Approval answers are sent as resume entries, see [Tool approval](#tool-approval).

### Updates

| AG-UI event | Update |
|---|---|
| `RUN_STARTED` | update without contents, `RawRepresentation` is the `RunStartedEvent`; `AgentConversation.TryGetThreadId` reads the thread |
| `TEXT_MESSAGE_*` | `TextContent` of the assistant, with the message id; the texts of several messages of a run have different message ids |
| `TOOL_CALL_*`, `TOOL_CALL_RESULT` | `FunctionCallContent` (`InformationalOnly`, the harness ran it) and `FunctionResultContent` |
| `MESSAGES_SNAPSHOT` | update without contents, `RawRepresentation` is the `MessagesSnapshotEvent`; `AgentConversation.TryGetSnapshot` reads the conversation |
| `RUN_FINISHED` with outcome `interrupt` | one `ToolApprovalRequestContent` per tool approval the run waits for |
| `RUN_ERROR` | `ErrorContent` with the message and code of the harness, which sends no details |
| any other type | ignored by the formatter of the SDK, also types defined after this client, so that newer servers can send more events |

`GetResponseAsync` collects the updates into a `ChatResponse` whose `RawRepresentation` is the snapshot of the run, if
the harness sent one (`AgentConversation.TryGetSnapshot(response, ...)`). The transport stops reading at `RUN_FINISHED`
or `RUN_ERROR`; nothing the harness sends after the end changes the result.

## Conversation

The harness is stateless: a run sends the whole conversation. At the end of each successful run the harness sends a
`MESSAGES_SNAPSHOT` with the conversation as it used it: the messages sent, the messages of an MCP prompt, the summary
of a compacted history, followed by the answer and the tool calls and results of the run. The next run sends that
conversation followed by the new user message:

```csharp
List<ChatMessage> conversation = [new ChatMessage(ChatRole.User, question)];
ChatResponse response = await client.GetResponseAsync(conversation, new ChatOptions { ConversationId = threadId });
if (AgentConversation.TryGetSnapshot(response, out List<ChatMessage>? snapshot))
{
    conversation = snapshot;
}
conversation.Add(new ChatMessage(ChatRole.User, nextQuestion));
```

A run that fails (`ErrorContent`, or an exception) sends no snapshot; the caller keeps the conversation it sent. A
run that failed after it ran a tool is the exception: it sends a snapshot up to the last tool result before its error.
For a run that failed or ended without answer text, `AgentConversation.GetConversationUpToToolResults` returns the
conversation to keep: the snapshot up to its last tool result if the run ran tools (trailing model messages without
text left out), else null, and the caller keeps the conversation it sent. The harness takes only user texts, assistant texts and tool calls, and tool results from the conversation; system and
developer messages and other contents are dropped (see [Conversation](ai-harness.md#conversation)).

## Tool approval

A tool that is not read-only runs only after the user approved it. The run then ends with one
`ToolApprovalRequestContent` per tool call, which is not part of the snapshot. The next run sends the snapshot followed
by one user message with the answers (`ToolApprovalRequestContent.CreateResponse(approved, reason)`). The AG-UI chat
client turns them into one `resume` entry per answer (`{ "interruptId": ..., "payload": { "approved": ..., "toolCall":
{ "callId": ..., "name": ..., "arguments": { ... } } } }`, see [Tool confirmation](ai-harness.md#tool-confirmation)).
The harness keeps no state of the interrupted run and runs an approved call as the resume entry names it, with the
user's JWT; a client must therefore only send the answers of the user. Sending a new user message instead drops the
pending approvals.

## Failures

A run that cannot be done throws an `AgentClientException` from the enumeration of the updates; its message never
contains the access token.

| Cause | `AgentClientFailure` |
|---|---|
| no token, a token with white space or control characters, or 401 | `Unauthorized` |
| 403 | `Forbidden` |
| 429 | `RateLimited`, `RetryAfter` from the `Retry-After` header (delay or date) |
| 413, e.g. a conversation too long to be sent again | `RequestTooLarge` |
| 404, 502, 503, 504, or the harness cannot be reached (`HttpRequestException`, e.g. connection refused or TLS failure, as inner exception) | `Unavailable` |
| any other status, e.g. 500 while no AI provider is configured, or a redirect | `ServerError` |
| the run takes longer than the run timeout | `Timeout` |
| the response is no AG-UI event stream (malformed JSON, an event without type), breaks the AG-UI event lifecycle, ends or breaks off before the run finished, or exceeds the size limit | `ProtocolError` |

A run cancelled by the caller's `CancellationToken` ends with an `OperationCanceledException`.

## Timeouts and limits

- The run timeout (constructor argument of the factory, default `kDefaultRunTimeout` of 5 minutes) bounds the whole
  run, including reading the events. An HTTP client of its own has no timeout of its own (`Timeout.InfiniteTimeSpan`);
  the timeout of a given HTTP client applies as well.
- A response may have at most 64 MiB (the answer, the tool calls and the snapshot); a run beyond it is a protocol error.
  The formatter buffers each event until it ends, so the response size bounds a single event as well.
- The harness limits the request body (default 10 MiB, see [Limits](ai-harness.md#limits)), and with it the length of
  the conversation; a longer one is refused with 413. The harness compacts a long conversation, so the snapshot it
  returns stays below the context window of the model; a history of more than 10000 messages is compacted as well.

## Security

- The client accepts any absolute http or https middleware server uri; another uri is refused with an
  `ArgumentException` that does not name it. It does not refuse plain http itself, because the UI sends the same JWT
  to the same uri with every other middleware call, and because the chat is part of the layout, so a refusal would
  make every page fail. Clear text is prevented on the receiving side instead: the middleware server refuses to start
  on a plain http listener that is not loopback (`EndpointGuard.RequireHttpsOrLoopbackListener` in `FWO.Basics`), and
  the installer addresses it over https through its Apache reverse proxy.
- The token is only sent as bearer token; it is never logged and never part of an exception message.
- An HTTP client of its own validates the server certificate against the trust store of the host, where the installer
  puts the FWO internal CA, does not follow redirects, so that the token reaches no other endpoint, and renews its
  pooled connections every 2 minutes.
- The answers, the tool calls and the snapshot of a run are written by the model, which also reads data entered by
  other users (e.g. the reason of a connection). They are untrusted: a UI must render them as text (Razor `@value`),
  never as markup.
- The harness and the MCP server apply the user's roles and permissions to every run; the client adds no rights. Tools
  that change data only run after an approval answer, which the harness takes from the client as it is.

## Reason check

`ConnectionReasonCheck.ToPromptRequest` creates the request of the MCP prompt `check_connection_reason`
(`ConnectionReasonCheck.kPromptName`):

| Argument | Value |
|---|---|
| `reason` | the reason, trimmed; blank or longer than 4000 characters (`ReasonCheckLimits.kMaxReasonLength`) throws an `ArgumentException` |
| `kind` | always sent: `connection`, `interface` or `interface_usage` (`ConnectionReasonKind`) |
| `name`, `app`, `source`, `destination`, `service` | trimmed and cut to 10000 characters (`ReasonCheckLimits.kMaxContextLength`), a cut value ending with `…` |
| `language` | language of the answer, e.g. `English` or `German`; letters, spaces and hyphens starting with a letter, at most 32 characters (`ReasonCheckLimits.kMaxLanguageLength`), otherwise an `ArgumentException`; the language of the reason if not given |

Arguments that are null or blank are left out. The MCP prompt asks for a plain-text answer whose first line is
`Verdict: GENUINE`, `Verdict: INSUFFICIENT` or `Verdict: NOT_GENUINE` in English, followed by the sections in the
language of the answer, see [Prompts](ai-mcp.md#prompts).

The reason check runs the prompt as a new conversation, without messages. The harness refuses a run of the reason
check that sends a conversation along and offers the reason check no tools (see
[AI harness](ai-harness.md#ai-functions)).

`ReasonVerdictParser.Parse` reads the verdict from the answer: the word of the verdict line (also with markup or
numbering, e.g. `1. **Verdict:** NOT_GENUINE`), else the first standalone verdict word in upper case (`NOT_GENUINE` and
`NOT GENUINE` are never read as `GENUINE`), else `ReasonVerdict.Unknown`. It works on partial answers while they are
streamed: the verdict stays unknown until its word is complete. The patterns are source-generated regular expressions
with a match timeout.

The prompt name, the argument names, the kinds, the limits and the language pattern must stay in step with
`RequestApplicationPrompts` of the MCP server, the AG-UI path with `HarnessServerSetup.kAgUiPath` of the harness and
`ai_harness_agui_path` of the installer, and the forwarded prompt properties with `PromptRequest` of the harness.
`ClientConformanceTest` checks them.

## AI chat in the UI

The UI offers a chat with the FWO agent on every page. It is shown only if the chat is switched on in the AI settings
(`aiChat`), allowed there for one of the roles of the user (`aiChatRoles`, every user if empty) and an AI provider is
configured (`AiChatDisplay.IsAvailable`); the harness refuses chat runs while the switch is off or the user holds none
of the roles, whatever the UI shows (see [AI harness](ai-harness.md#ai-functions)). The button of the reason check is
shown in the same way (`aiReasonCheck`, `aiReasonCheckRoles`).

| Part | Role |
|---|---|
| `AiChatSession` (`Services`, scoped) | The chat of one circuit: entries, conversation, thread, runs, approvals, state of the panel, draft |
| `AiChatRun` | Collects the updates of a run: answer text (`AgentAnswerText`, shared with the reason check), approval requests, error, thread, snapshot |
| `AiChatPanel` (`Shared`, in `MainLayout`) | Panel docked on the right below the navigation bar, a resizable `Sidebar` above the page and the report sidebars (`.ai-chat-sidebar`, z-index 14) |
| `AiChatNavButton` (in `NavigationMenu`) | Opens and minimizes the panel; badge for a running answer, a new answer and pending approvals |
| `AiChatFooterOffset` (in `MainLayout`) | Ends the UI messages of the footer left of the panel while it is open, so that they do not cover its input |

The session follows [Conversation](#conversation): every run sends the snapshot of the previous run followed by the new
user message (at most 10000 characters, `AiChatSession.kMaxMessageLength`) on the thread of the previous run. A run
without snapshot keeps the messages it sent plus its answer. A failed or cancelled run leaves the conversation as it was
before the run, so the next message is sent without the failed one; the answer shows the failure with the texts of the
reason check (`AiReasonCheckDisplay.FailureMessage`). A run that failed or ended without answer after it ran tools, e.g.
an approved call after which the provider failed, keeps its tool calls and results in the conversation
(`AgentConversation.GetConversationUpToToolResults`), so that the AI knows of them and does not ask for them again.

A run ending with tool approval requests keeps its snapshot and shows each call with its tool name and its arguments
(indented JSON, cut to 2000 characters). No new message can be sent until the user approved or rejected every call;
then one run sends the snapshot followed by one user message with all answers, see [Tool approval](#tool-approval).

The conversation lives only in the memory of the circuit: it survives navigation and minimizing, a run goes on in the
background, and it is lost on reload, logout or "New Chat". It is dropped (and the panel minimized) when another user,
or no user, is logged in to the circuit, compared by the DN claim `x-hasura-uuid`. Runs raise `AiChatSession.Changed`
from their own thread, at most every 150 ms while an answer streams; the components re-render through their
dispatcher. All texts of the chat are rendered as Razor text; answers keep their line breaks (`white-space: pre-wrap`).
The panel scrolls to the newest entry with `scrollToEndIfNear` of `window.js`, unless the user has scrolled up.
The `Sidebar` renders its content only once it knows the height of the navigation bar, after the first render of the
panel. The panel therefore waits for the `ContentShown` callback of the `Sidebar` before it focuses the input and
scrolls to the newest entry.

## Deployment

The library is part of `roles/lib`, which the installer copies to `{{ fworch_home }}/lib/files` on the UI and
middleware hosts. It runs in the process of its user, e.g. the UI, and needs no service of its own. The harness is
reached through the Apache of the middleware at `https://<middleware host>:8888/agui`.

## Tests

- `roles/tests-unit/files/FWO.Test`, with a fake HTTP handler (`AgUiTestDoubles`):
  - `FwoAgentChatClientFactoryTest`: the endpoint guard, the requests of the runs (prompt, thread, conversation,
    resume entries), the token asked for every run, the mapping of the statuses including `Retry-After`, connection
    failures, the run timeout versus the cancellation by the caller, and the disposal of the HTTP client.
  - `FwoAgentChatClientEventStreamTest`: the server-sent event format, events split across reads, text passed on while
    the response streams, several messages, interrupts, run errors, snapshots, unknown events, protocol errors and the
    size limit.
  - `ConnectionReasonCheckTest`, `ReasonVerdictParserTest` and `AgUiBoundedReadStreamTest`.
  - the AI chat of the UI, with a scripted chat client (`AiChatTestDoubles`): `UiAiChatSessionTest` (conversation and
    thread across runs, approvals, failures, cancelling, new chat, change of the user), `UiAiChatRunTest`,
    `UiAiChatDisplayTest`, and the components in `UiAiChatPanelTest` (including the output encoding),
    `UiAiChatNavButtonTest` and `UiAiChatFooterOffsetTest`.
- `roles/tests-unit/files/FWO.Ai.Tests`:
  - `ClientHarnessTest`: the client against the AG-UI endpoint of the harness pipeline on a test server, with the MCP
    server pipeline on another one: a reason check with language and its snapshot, a conversation sent again, interrupt
    and resume with approval and rejection and the run after it, a run error without snapshot, and runs refused with
    401, 403 and 429.
  - `ClientConformanceTest`: the constants shared with the harness and the MCP server, and that the MCP server offers
    every argument the reason check sends.
