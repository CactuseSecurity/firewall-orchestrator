# AI harness (FWO.Ai.Harness)

`roles/ai/files/FWO.Ai.Harness` is an ASP.NET server that runs an AI agent against the FWO MCP server
(`roles/ai/files/FWO.Ai.Mcp`, see [AI MCP server](ai-mcp.md)) and offers it to user interfaces over
[AG-UI](https://docs.ag-ui.com/) (Agent-User Interaction Protocol). It is built on the
[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) (`Microsoft.Agents.AI`, `ChatClientAgent`,
AG-UI hosting `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`), which itself is built on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A UI posts
AG-UI runs with the JWT of the user and receives the answer as server-sent events. The FWO UI does so with the AG-UI
chat client of [FWO.Ai.Client](ai-client.md), e.g. to check the functional reason of a modelled connection.

See [Deployment](#deployment) for how the installer deploys it.

| Feature | Provided by |
|---|---|
| UI protocol | AG-UI endpoint `POST /agui` (`MapAGUIServer` of the Agent Framework), answers as server-sent events |
| AI API abstraction | `IChatClient` of Microsoft.Extensions.AI, created from the provider SDKs: `OllamaSharp`, `Microsoft.Extensions.AI.OpenAI`, `Anthropic`, `Google.GenAI` |
| Agent | One `ChatClientAgent` shared by all users (`FwoAgentFactory`), keeping no history; per request an `FwoUserAgent` with the user's JWT |
| Conversation | Stateless: every run sends the whole conversation (`TurnInput`, `ConversationFilter`), and the harness returns it at the end of the run as `MESSAGES_SNAPSHOT` (`ConversationSnapshot`) |
| Tool management | The tools of the FWO MCP server that the user may call, loaded with the user's JWT for each run (`McpClientTool` of the MCP C# SDK) |
| Prompt management | A run names an MCP prompt in `forwardedProps.prompt` |
| Instructions | The MCP prompt `fwo_agent_instructions`, got with the user's JWT for each run and sent as instructions (system prompt) |
| Agent loop | Function invocation of Microsoft.Extensions.AI, configured by the harness |
| Tool confirmation | AG-UI interrupt for every tool that is not read-only, answered by a resume entry |
| AI functions | Chat and reason check, switched on and off in the AI settings; runs of a function switched off are refused (`AgentFeatureGate`) |
| Context compaction | Summary of the older history by the model, triggered by the estimated size of the history |

## AG-UI endpoint

`POST /agui` takes an AG-UI `RunAgentInput` and answers with the AG-UI events of the run (`RUN_STARTED`,
`TEXT_MESSAGE_*`, `TOOL_CALL_*`, `MESSAGES_SNAPSHOT`, `RUN_FINISHED` or `RUN_ERROR`). The request needs
`Authorization: Bearer <JWT>` with a JWT of the middleware server (see `FWO.Ai.Harness.http`):

```json
{
    "threadId": "thread-1",
    "runId": "run-1",
    "messages": [ { "id": "message-1", "role": "user", "content": "Which roles do I have?" } ]
}
```

- **Client tools** (`tools` of the input) are not supported; such a run fails with `RUN_ERROR`.
- **Errors** reach the client as `RUN_ERROR` without details; the harness logs them.
- `state` and `context` of the input are ignored.

### Conversation

The harness is stateless: it keeps no conversation, and a run sends the whole conversation in `messages`. A run takes
from it (`ConversationFilter`):

- user messages with texts, assistant messages with texts and tool calls, and tool messages with tool results; the
  contents are copied, only their values are taken over,
- not: system and developer messages, messages of other roles, and other contents (images, reasoning, metadata), so
  that the instructions only come from the MCP server,
- not: a tool call without a later result, or a result without an earlier call, as the providers reject such a history.

The new input of a run (`TurnInput`) is one of:

- the last message, a user message; its length is bounded by the request body limit only (see [Limits](#limits)), as
  it may hold large data such as the output of a tool,
- an MCP prompt, with no new user message (see [MCP prompts](#mcp-prompts)),
- the answers to the tool approvals of the interrupted run (see [Tool confirmation](#tool-confirmation)), with the
  conversation of that run and no new user message.

A run with none of them, or with more than one, fails with `RUN_ERROR`. The messages before the new input are the
history, which is compacted if it is too large (see [Context compaction](#context-compaction)).

At the end of a successful run, right before `RUN_FINISHED`, the harness sends a `MESSAGES_SNAPSHOT` with the
conversation as it used it: the history (compacted, if it was), the new input (the messages of an MCP prompt instead of
its name), and the response of the run with its tool calls and results. The client sends it with its next run, followed
by the new user message. Left out of the snapshot are the tool approvals, as an open approval reaches the client as
interrupt and an answered one as the tool call and result it caused, and the tool calls the agent loop left unanswered.
A failed run sends no snapshot, so the client keeps the conversation it sent. The AG-UI hosting closes the last text
message of the run only after the snapshot, which AG-UI allows.

The `threadId` only groups the runs of a conversation for the client; a run without one gets a new id, which the
client reads from `RUN_STARTED`. As the harness keeps no state of a thread, several runs on the same thread may run at
the same time.

A user can only mislead the agent of their own conversation: the harness takes the conversation of the client as it is,
but its tools still run with the user's JWT.

### MCP prompts

A run can send a prompt of the MCP server as the user's turn instead of a text:

```json
{
    "threadId": "thread-2",
    "runId": "run-1",
    "messages": [],
    "forwardedProps": {
        "prompt": { "name": "check_access_request_reason", "arguments": { "reason": "..." } }
    }
}
```

The harness gets the prompt from the MCP server with the user's JWT, so the MCP server decides which prompts the user
may get and checks the arguments. Arguments are strings; at most 16 arguments and a name of at most 128 characters are
accepted, the values are bounded by the request body limit only. A run naming a prompt sends the conversation so far
and no new user message. The prompts a user may get are listed by the MCP server itself (`prompts/list`), not by the
harness.

### AI functions

The AI settings switch two functions on and off: the chat (`aiChat`) and the reason check (`aiReasonCheck`), both off
by default (see [Settings](#settings)). Each function can be limited to roles (`aiChatRoles`, `aiReasonCheckRoles`): a
user may run it only if the JWT of the request holds at least one of these roles (claim `role`); no roles allow every
user. Right after reading the input of a run (`TurnInput`), before connecting to the MCP server, the agent checks the
function the run belongs to (`AgentFeatureGate`):

- the MCP prompt `check_connection_reason` belongs to the reason check (`AgentFeatureGate.kReasonCheckPromptName`; keep
  it in step with the MCP server and with `ConnectionReasonCheck.kPromptName` of the client, `ClientConformanceTest`
  checks it),
- everything else belongs to the chat: a new user message, the answers to tool approvals and any other MCP prompt. A
  new prompt without a switch of its own is thus refused while the chat is off or not allowed for the user (fail
  closed).

A run of a function switched off fails with `RUN_ERROR` (`AgentFeatureDisabledException`, logged as warning), a run of a
user holding none of its roles likewise (`AgentFeatureNotPermittedException`); neither reaches the MCP server or the AI
provider. The switches and roles hold for every client of the AG-UI endpoint, not only for the UI, which hides the
functions that are off or not allowed for the user. A stored role list that is not a JSON array of role names switches
its function off (`AiFunctionRoles.TryParse`), so that it is not offered to every user by mistake. As the roles are read
from the JWT, a changed role assignment applies with the next JWT of the user. A change of a switch or of the roles
replaces the agent factory like any other change of the AI settings.

### Tool confirmation

Every MCP tool that the server does not mark as read-only (`[McpServerTool(ReadOnly = true)]`) requires the
confirmation of the user. Instead of running the tool, the run streams the tool call (`TOOL_CALL_START`, `TOOL_CALL_ARGS`,
`TOOL_CALL_END`) and finishes with an interrupt:

```json
{ "type": "RUN_FINISHED", "outcome": { "type": "interrupt", "interrupts": [ { "id": "<interrupt id>", "reason": "tool_call", "toolCallId": "<call id>" } ] } }
```

The client asks the user and answers every interrupt in the next run, which sends the conversation of the snapshot of
the interrupted run and one resume entry per answer:

```json
{
    "threadId": "thread-1",
    "runId": "run-2",
    "messages": [ { "id": "message-1", "role": "user", "content": "Store the value 42." } ],
    "resume": [ { "interruptId": "<interrupt id>", "payload": { "approved": true, "toolCall": { "callId": "<call id>", "name": "<tool>", "arguments": { } } } } ]
}
```

A rejected tool is not run; the model gets the rejection and its reason. As the harness keeps no state of the
interrupted run, it trusts the answers of the client: an approved call runs with the tool and the arguments the resume
entry names, also if the agent never asked for it. The binding of answers to recorded requests of the Agent Framework
(`ApprovalResponseBindingChatClient`) is disabled, as it needs the session of the interrupted run. What an approved call
can do is still bounded by the tools the MCP server offers the user and by the user's JWT; a call of a tool the user is
not offered gets an error result. A run that sends a new user message instead of the answers drops the open approvals.

Known limitation: with Gemini, if a reply holds text and the call of a tool requiring confirmation, the resumed request
holds two assistant messages in a row, which Gemini rejects. The cause is the function invocation of
Microsoft.Extensions.AI (`FunctionInvokingChatClient`), which puts the call into an assistant message of its own after
the text. The harness has no workaround, as Gemini is not used for now.

In a response calling a read-only tool and a tool requiring confirmation, only the latter is asked for. The Agent
Framework keeps the approval of the read-only call in the session of the run, which does not outlive it, so the
read-only call of that response is dropped when the answer arrives; the model can call it again.

A new tool that changes data is confirmed even if nobody remembers to configure it (fail closed).

### Limits

- Only JWTs of users holding an FWO role that is neither technical nor anonymous and carrying the user's directory
  identity (`x-hasura-uuid`) are accepted, like by the MCP server (401 without, 403 otherwise). The directory identity
  partitions the limits of a user.
- `RunsPerMinute` (default 10) runs per user within any minute; more are rejected with 429 and `Retry-After`. Each
  approval answer is a run.
- `MaxConcurrentRunsPerUser` (default 3) runs per user at a time; one more is rejected with 429 right away.
- `MaxRequestBodyBytes` (default 4 MiB, at most 64 MiB) per request (413 otherwise). It bounds the conversation a run
  sends; the compaction keeps the conversation the harness returns below the context window of the model, so it can be
  sent again. At most 1000 messages per run; the new user message has no length limit of its own.

## Agent loop

The loop is the function invocation of Microsoft.Extensions.AI, configured by the harness:

- `MaxIterationsPerRequest` (default 10) limits the model requests per user turn. If the model still calls tools in the
  last of them, it is asked once more without tools for its answer. A tool call it makes there gets no result and is
  not part of the snapshot.
- `MaxConsecutiveErrorsPerRequest` (default 3) is the number of failed tool call rounds in a row before the turn fails.
- Exception details of failed tools are not passed to the model.
- Tools are not called concurrently.
- Each run connects to the MCP server with the JWT of its request and loads the user's tools and the instructions; the
  connection is closed when the run ends.
- The instructions are the text of the MCP prompt `fwo_agent_instructions` (`FwoUserAgent.kInstructionsPromptName`,
  keep it in step with the MCP server). They are sent with every request of the run and are not part of the
  conversation. A run fails if the MCP server does not offer the prompt.

## Context compaction

Before each turn, the harness checks the history the run sends (the conversation before the new input). If its
estimated size exceeds `ContextWindowTokens - CompactionReserveTokens`, all older messages are summarized by the same
model in one call (without tools):

- The 4 most recent message groups stay verbatim. A tool call is never separated from its result. Nothing else is
  dropped.
- The summary is a user message starting with `[Summary]`, at the start of the history. The Agent Framework writes it
  as an assistant message, but providers expect a history to start with a user message, and Gemini rejects a tool call
  right after a model message.
- The snapshot of the run holds the summary instead of the summarized messages, so the client sends the summary with
  its next runs and it is written once. Later summaries include the previous summary.
- Tokens are estimated (UTF-8 bytes / 4). The instructions and the tool schemas are not counted, so the reserve must
  hold one full turn (instructions, tool schemas, the new message, the tool results of that turn and the answer) as well
  as the summary call.
- If the summary call fails, the history stays as it is and the next run tries again.
- The summary prompt asks to keep names, UIDs, IP addresses, networks and ports exactly.

The compaction types are experimental in the Agent Framework (diagnostic `MAAI001`, suppressed in the project file).
The AG-UI hosting packages are only published as previews.

## Settings

The harness has two kinds of settings:

- **Server settings** are read at startup from the configuration section `Harness`: environment variables (e.g.
  `Harness__McpEndpoint`) and the command line. The server refuses to start if they are invalid.
- **AI settings** (provider and agent loop) are set by an admin on the AI settings page of the UI (`Settings > AI
  settings`, `/settings/ai`) and stored as global config items. The API key is a credential of the credential store
  (`Settings > Credentials`), which the AI settings reference. The harness reads them at startup and again every
  `SettingsRefreshSeconds`, so a change applies without a restart.

Server settings:

- `McpEndpoint` (default `http://127.0.0.1:8890/mcp`, the MCP server on the same host), `McpConnectionTimeout`
  (optional), `RunsPerMinute`, `MaxConcurrentRunsPerUser` and `MaxRequestBodyBytes`, see above.
- `TechnicalUser` (default `ai-harness`) and `TechnicalUserPasswordFile` (default
  `/usr/local/fworch/etc/secrets/ai_harness_pwd`): the technical user the harness reads the AI settings with, see
  [Reading the AI settings](#reading-the-ai-settings).
- `SettingsRefreshSeconds` (default 30, at most 3600): time between two readings of the AI settings.

AI settings (`FWO.Data.Ai.AiSettings`, config keys in `AiSettings.kKeys`):

| Setting | Config key | Meaning |
|---|---|---|
| Provider | `aiProvider` | `None` (default, the agent is not available), `Ollama`, `OpenAi`, `Anthropic`, `Google` |
| Model | `aiModel` | Model id known to the provider; required |
| Endpoint | `aiEndpoint` | Optional base URL, e.g. of an OpenAI compatible server; Ollama defaults to `http://localhost:11434`. Must be https if an API key or access token is sent to a host other than the local one |
| Authentication | `aiAuthMethod` | `ApiKey` (default) or `OAuth2ClientCredentials`, see [OAuth 2.0](#oauth-20) |
| Token URL | `aiTokenUrl` | Full URL of the OAuth 2.0 token endpoint, e.g. `https://sso.example.com/realms/<realm>/protocol/openid-connect/token` for Keycloak; required for OAuth 2.0, https unless a loopback address |
| Credential | `aiCredentialId` | Id of the credential holding the API key as its secret, or for OAuth 2.0 the client id as user name and the client secret as secret; required except for Ollama. Key pairs are not offered |
| Context window | `aiContextWindowTokens` | Context window of the model in tokens; required |
| Max output tokens | `aiMaxOutputTokens` | 0 for the provider's default; must be smaller than the context window |
| Max iterations | `aiMaxIterationsPerRequest` | Model requests per user turn (default 10), see [Agent loop](#agent-loop) |
| Max consecutive errors | `aiMaxConsecutiveErrorsPerRequest` | Failed tool call rounds in a row before a turn fails (default 3) |
| Compaction reserve | `aiCompactionReserveTokens` | 0 for a quarter of the context window; must be smaller than the context window and larger than the max output tokens if those are set |
| AI chat | `aiChat` | `True` lets the users chat with the agent, see [AI functions](#ai-functions); off (`False`) by default and for any other value |
| AI check of reason | `aiReasonCheck` | `True` lets modellers run the reason check of a connection; off (`False`) by default and for any other value |
| Roles of the AI chat | `aiChatRoles` | JSON array of the role names allowed to chat, e.g. `["admin","modeller"]`; `[]` (default) for every user, see [AI functions](#ai-functions) |
| Roles of the reason check | `aiReasonCheckRoles` | JSON array of the role names allowed to run the reason check; `[]` (default) for every user |

The settings page checks the settings the way the harness does (`AiSettings.Validate`) and does not store invalid ones.
The harness checks them again when it reads them: without a provider, or with settings it cannot use, every run fails
with HTTP 500 and the reason is logged once per change (e.g. a missing credential), never with the API key. If the
settings cannot be read at all, e.g. while the API is not reachable, the last usable settings stay in effect.

### OAuth 2.0

For an OpenAI compatible server behind an authorization server such as Keycloak, the harness can authenticate with an
access token instead of an API key (`aiAuthMethod` = `OAuth2ClientCredentials`, provider `OpenAi` with an endpoint
only):

- The token is requested with the client credentials grant (RFC 6749, 4.4) from `aiTokenUrl`, with client id and
  secret in the request body (`client_secret_post`) and without scope. The token client lives in FWO.Basics
  (`FWO.Basics.OAuth.OAuthTokenClient`) and is shared with the Azure login of the autodiscovery and of the firewall
  write library. It follows no redirects, does not decompress answers, reads at most 64 KB, times out after 30 seconds
  and always checks the certificate of the token endpoint in the harness.
- `CachedOAuthTokenProvider` keeps the token until 30 seconds before it expires (`expires_in`; a token without expiry
  for a minute, a short-lived one for half its lifetime); parallel runs share one renewal.
- `OAuthBearerPolicy` sets the token as `Authorization: Bearer` on every request of the OpenAI client. If the server
  answers 401, the token is dropped and the request is sent once more with a new token.
- The token provider belongs to the chat client, so a change of the AI settings, e.g. of the credential, starts with a
  new token.

### Reading the AI settings

The harness reads the AI settings with its own identity, the technical LDAP user `ai-harness` with the technical role
`ai-harness` (`Roles.AiHarness`):

- `MiddlewareTechnicalUserLogin` logs it in at the middleware server on the same host (`AuthenticationToken/GetTokenPair`,
  `ConfigFile.MiddlewareServerNativeUri`) with the password from `TechnicalUserPasswordFile`, which is read at every
  login. `FwoAiConfigReader` keeps the token until two minutes before it expires and renews it with the refresh token,
  or by a new login.
- With that token it reads the global config items of `AiSettings.kKeys` (`ai/getAiSettings.graphql`) and the
  credential they reference from the view `v_ai_provider_credential` (`ai/getAiProviderCredential.graphql`).
  `FwoAiSettingsSource` decrypts its secret with the main key of the host (`AesEnc.GetMainKey`).
- `AiSettingsRefresher` creates a new agent factory when the settings, or the API key, have changed.
  `AgentFactoryHolder` hands the current factory to each run; a replaced factory is disposed when the last run using it
  has ended. `AiSettingsRefreshService` runs the refresher in the background.

The role `ai-harness` may only read the global config items of `AiSettings.kKeys` and the view
`v_ai_provider_credential` (`roles/database/files/sql/idempotent/fworch-ai.sql`), which returns the one credential the
config item `aiCredentialId` names. It cannot read any other credential or config item, nor change anything. Being a
technical role, it is refused by the AG-UI endpoint and the MCP server (`Roles.IsTechnicalOrAnonymous`).

The JWT public key is read from the FWO config file (`ConfigFile.JwtPublicKey`), like by the MCP server.

## Security

- The MCP server is called with the JWT of the request, so the tools are subject to that user's roles and permissions.
  The harness adds no rights of its own.
- The harness keeps no conversations, so it holds no firewall data between runs. The client holds the conversation,
  including the tool results, i.e. firewall data the user may read.
- The AG-UI input of a client never reaches the AI provider unfiltered: the agent passes only the kept conversation, the
  new input and the user's tools on, never system or developer messages or options of the client.
- A JWT, an API key, an OAuth client secret or an access token is only sent over https, or over http to a loopback
  address.
- The API key or the OAuth client secret is stored encrypted in the credential store and never in the environment of
  the service. The harness reads it with a technical user that can read this one credential only, and keeps it in
  memory, like the access token; neither is logged.
- Exception details of failed tool calls are not passed on to the model, and run errors not to the client.
- Tools that are not marked read-only need an approval answer before they run. The harness takes the answer, and the
  call it names, from the client: the client acts for the user with the user's JWT, which bounds what the call can do.
- Tool results, the replies of the model and the conversation a client sends are untrusted data. The summary is
  written by the same model and replaces the summarized messages, so text injected into them, e.g. through a tool
  result, can carry over into it.

## Deployment

The installer deploys the harness with the MCP server (`install_ai_server`, see
[install-advanced.md](../../installer/install-advanced.md)): `roles/ai` publishes `FWO.Ai.Harness` on the middleware
host and runs it as the service `fworch-ai-harness` (`roles/ai/templates/fworch-ai-harness.service.j2`).

- It listens on `ai_harness_native_uri` (loopback, port `ai_harness_internal_port`, default 8891) and the Apache vhost
  of the middleware publishes the AG-UI endpoint under the same path (`ai_harness_agui_path`, `/agui`):
  `https://<middleware host>:8888/agui`. Keep `ai_harness_agui_path` in step with `HarnessServerSetup.kAgUiPath`.
- The unit sets `McpEndpoint` to `ai_mcp_native_mcp_uri` (`Harness__McpEndpoint`), the MCP server the installer deploys.
- The installer does not configure the AI provider, so runs fail until an admin sets it on the AI settings page, see
  [Settings](#settings).
- The installer creates the technical LDAP user `ai-harness` (`ai_harness_user_dn`, in `tree_operators.ldif.j2` and the
  9.9.4 LDAP upgrade) with a random password in `ai_harness_password_file` (`/usr/local/fworch/etc/secrets/ai_harness_pwd`,
  readable by the fworch user only), and sets it in LDAP on every run (`roles/middleware/tasks/set_ai_harness_password.yml`).
  The unit passes the user, the file and `ai_harness_settings_refresh_seconds` (`Harness__TechnicalUser`,
  `Harness__TechnicalUserPasswordFile`, `Harness__SettingsRefreshSeconds`).
- Like the MCP server, the unit hides the JWT signing key of the middleware (`InaccessiblePaths`).
- An uninstallation stops the service and removes its unit and files. Setting `install_ai_server=no` on an existing
  installation only removes `/agui` from the Apache vhost.

## Tests

The unit tests are in `roles/tests-unit/files/FWO.Ai.Tests`. They run the harness against the MCP server pipeline on a
test server, with a scripted chat client instead of a real AI provider, both directly (`FwoUserAgent`) and through the
AG-UI endpoint on a second test server (`HarnessServerTest`). `HarnessTurnInputTest`, `HarnessConversationFilterTest`
and `HarnessConversationSnapshotTest` test what a run takes from the conversation, what it sends to the provider and
what it returns. `HarnessAgentFeatureGateTest` tests the switches and roles of the AI functions, in the agent and through the
AG-UI endpoint. A strict mode of the scripted chat client rejects requests the way
the providers do (`ProviderHistoryRules`).
`HarnessFwoAiConfigReaderTest`, `HarnessFwoAiSettingsSourceTest`, `HarnessAiSettingsRefresherTest`,
`HarnessAgentFactoryHolderTest` and `HarnessTechnicalUserLoginTest` test reading and applying the AI settings;
`AiHarnessAccessGuardTest` in FWO.Test checks the Hasura permissions of the role `ai-harness`.
`ClientHarnessTest` runs the chat client of the UI (`FWO.Ai.Client`, see [AI client](ai-client.md)) against the same
endpoint, over several runs of a conversation and through an interrupt. Because they reference the harness sources,
the installer copies `FWO.Ai.Harness` to the middleware host next to `FWO.Ai.Mcp` (`roles/ai`).

The integration test `roles/tests-integration/tasks/test-ai-harness.yml` checks the installed service: its loopback
listener, the hidden JWT signing key, and that the harness and its Apache endpoint refuse runs without a token (401) and
of a user without an FWO role (403). It never sends a run of an FWO user, as that would reach the AI provider once one
is configured.
