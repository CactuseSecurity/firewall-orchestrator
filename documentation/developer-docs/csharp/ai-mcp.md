# AI MCP server (FWO.Ai.Mcp)

`roles/ai/files/FWO.Ai.Mcp` is an ASP.NET server that offers FWO tools and prompts to AI clients over the
[Model Context Protocol](https://modelcontextprotocol.io/) (MCP). It is built on the
[MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`). Its clients are
MCP clients such as Claude Code or Codex, and the [FWO AI harness](ai-harness.md), which runs an AI agent on its tools.
A client sends the JWT of the user, and the server offers that user only the tools and prompts their roles allow.

See [Deployment](#deployment) for how the installer deploys it.

| Feature | Provided by |
|---|---|
| Protocol | MCP over streamable HTTP, endpoint `POST /mcp` (`MapMcp` of the MCP C# SDK), stateless |
| Authentication | JWTs of the middleware server, validated with the JWT public key of the installation |
| Authorization | Default policy: an FWO role that is neither technical nor anonymous; `[Authorize]` per tool or prompt class, `[Authorize(Roles = ...)]` per tool (`AddAuthorizationFilters`) |
| Tools | `UserTools` (`get_current_user`), `RandomNumberTools` (`get_random_number`, a sample tool) |
| Prompts | `AgentPrompts` (`fwo_agent_instructions`), `RequestApplicationPrompts` (`check_access_request_reason`, `check_connection_reason`) |
| Limits | Request budget and concurrent requests per user (`McpRateLimiting`), request body size |
| Reverse proxy | Forwarded headers trusted from the local Apache only |

## MCP endpoint

`POST /mcp` takes MCP JSON-RPC requests (`tools/list`, `tools/call`, `prompts/list`, `prompts/get`, ...) and answers
with JSON or an event stream. The request needs `Authorization: Bearer <JWT>` with the access token returned by
`POST /api/AuthenticationToken/GetTokenPair` of the middleware server (see `FWO.Ai.Mcp.http`):

```http
POST https://<middleware host>:8888/mcp
Accept: application/json, text/event-stream
Content-Type: application/json
MCP-Protocol-Version: 2025-11-25
Authorization: Bearer <access token>

{ "jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": { "name": "get_random_number" } }
```

- **Stateless.** The server keeps no MCP session (`Stateless = true` of the HTTP transport): every request stands on
  its own and is authorized with its own token. The server cannot send requests to the client (e.g. sampling or
  elicitation).
- **Tools and prompts by role.** `tools/list` and `prompts/list` only list what the user may call. Calling a tool
  restricted to other roles is refused.
- **Clients.** Any MCP client that supports streamable HTTP and a custom `Authorization` header can connect, at
  `https://<middleware host>:8888/mcp`. The client has to trust the certificate of the middleware's Apache endpoint
  (issued by the FWO internal CA by default), and the access token expires like every middleware token, so the client
  needs a new one from time to time.

### Tools

| Tool | Roles | Result |
|---|---|---|
| `get_current_user` | every FWO user role | Name and FWO roles taken from the validated access token |
| `get_random_number` | `admin`, `auditor` | A random number between `min` (inclusive) and `max` (exclusive); a sample of a tool restricted to roles |

Both tools are marked read-only (`[McpServerTool(ReadOnly = true)]`). The AI harness asks the user to confirm every
tool that is not marked read-only before it runs, so mark a tool read-only only if it changes nothing.

To add a tool:

1. Add a class to `Tools/` with a method carrying `[McpServerTool]` and a `[Description]` for the tool and each
   parameter. The SDK names the tool after the method in snake case (`GetCurrentUser` becomes `get_current_user`).
2. Restrict it: `[Authorize]` on the class for every FWO user role, `[Authorize(Roles = ...)]` on the method for a
   subset (see `Roles` in `FWO.Basics`). The endpoint requires an FWO user role in any case.
3. Register the class in `McpServerSetup.AddFwoMcpServer` (`WithTools<T>()`).
4. A tool that reads FWO data must call the API with the JWT of the caller, so that the Hasura permissions (tenant,
   visible managements, editable owners) still apply. A tool must never use a technical identity of its own.
5. Add unit tests in `roles/tests-unit/files/FWO.Ai.Tests`, for the result and for the roles that may and may not
   call it.

### Prompts

`fwo_agent_instructions` (`AgentPrompts`, no arguments) returns the instructions (system prompt) of an AI agent that
supports FWO users with the tools of this server. The AI harness gets it with the user's JWT for each run and sends it
as instructions to its AI provider; keep `AgentPrompts.kAgentInstructionsPromptName` in step with
`FwoUserAgent.kInstructionsPromptName` of the harness.

The other prompts (`RequestApplicationPrompts`) check whether the justification of a requested firewall change is a
genuine one. They return the instructions for the model, with the arguments embedded as data. The FWO UI runs
`check_connection_reason` through the AI harness with the [AI client](ai-client.md), shows the answer as text and reads
the verdict from it.

| Prompt | Arguments |
|---|---|
| `check_access_request_reason` | `reason` (required), `title`, `source`, `destination`, `service`, `language` |
| `check_connection_reason` | `reason` (required), `kind` (`connection`, `interface` or `interface_usage`, default `connection`), `name`, `app`, `source`, `destination`, `service`, `language` |

The answer is plain text without Markdown or other markup (no `**`, `#`, tables or code blocks), so that clients can
show it as it is:

- The first line is `Verdict: GENUINE`, `Verdict: INSUFFICIENT` or `Verdict: NOT_GENUINE`. The heading and the verdict
  word are in English whatever the language of the answer, so that clients can read the verdict
  (`ReasonVerdictParser` of the AI client).
- Then the sections Criteria (one line per criterion), Questions to the requester and Improved justification, each
  starting with its heading on a line of its own. The questions and the improved justification are left out for
  `GENUINE`.
- The answer, including the headings of the sections, is in the language given by `language`, e.g. `English` or
  `German`, and in the language of the justification if none is given. The improved justification is always in the
  language of the justification.

- The arguments are entered by the requester and are untrusted. Each one is embedded as a data block tagged with its
  name, with `&`, `<` and `>` escaped, so that it cannot close its block and pass itself off as instructions. The
  instructions tell the model never to follow instructions in the data.
- `language` is the exception: it is chosen by the client (e.g. the UI language of the user), and it is embedded into
  the instructions. It is therefore only accepted as a language name of letters, spaces and hyphens starting with a
  letter, of at most 32 characters (`kLanguagePattern`, `kMaxLanguageLength`); anything else is refused with an MCP
  error, so that it can carry neither markup nor punctuation.
- `reason` may have at most 4000 characters, every other argument at most 10000. A longer argument or an unknown `kind`
  is refused with an MCP error.
- Arguments that are not given are left out.
- Keep the prompt names, argument names, limits and the language pattern in step with `ConnectionReasonCheck` and
  `ReasonCheckLimits` of the AI client (`ClientConformanceTest`).

## Limits

- Only JWTs signed by the middleware server of the installation, with its issuer and audience, not expired, and of a
  user holding at least one FWO role that is neither technical (`middleware-server`, `importer`, `dbbackup`) nor
  `anonymous` are accepted (401 without a valid token, 403 otherwise).
- Every request of a user counts against a budget of `rate_limit_requests_per_minute` (default 600) requests within any
  minute; the window moves on in steps of 10 seconds.
- At most `rate_limit_concurrent_expensive_requests` (default 2) requests of a user run at the same time; up to 4
  further requests wait, oldest first, and the rest are rejected.
- A user is identified by the `x-hasura-uuid` claim, otherwise by the user name. Refused requests (401, 403) do not
  reach the limits, so a request cannot use up the budget of a user it only names.
- Rejected requests get 429 with `Retry-After: 60` and are logged. Request bodies are limited to 1 MiB (413 otherwise).

## Settings

The MCP server has no settings file of its own. It reads from the FWO config file (`ConfigFile`):

- the JWT public key of the installation (`ConfigFile.JwtPublicKey`),
- `rate_limit_requests_per_minute` and `rate_limit_concurrent_expensive_requests` from `/etc/fworch/fworch.json`, the
  keys the middleware REST API reads its limits from, see [API request limits](../../auth/README.md#api-request-limits).
  They are read at startup; missing or non-positive values fall back to the defaults. Both servers count separately.

The URL it listens on is set by `ASPNETCORE_URLS`, which the installer puts into the unit. For development,
`Properties/launchSettings.json` runs it on `http://localhost:6296` and `https://localhost:5242`. As the JWT of the
user arrives there, it refuses to start if that URL binds plain http to anything but `localhost` or a loopback
address (`EndpointGuard.RequireHttpsOrLoopbackListener` in `FWO.Basics`): it logs the reason and exits with code 78.

## Security

- The server adds no rights of its own: a tool runs with the identity of the token, and the tools and prompts a user
  sees and may call depend on the roles in that token.
- The middleware server also signs anonymous bootstrap tokens and tokens of technical users; neither gives access.
- Forwarded headers (`X-Forwarded-Proto`, `X-Forwarded-For`) are only trusted from a loopback peer, i.e. the local
  Apache, and only the entry Apache appended is used (`ForwardLimit = 1`), so a client cannot choose its own address.
- The unit hides the JWT signing key of the middleware (`InaccessiblePaths`): the server runs as the same user as the
  middleware, but only needs the public key.
- Prompt arguments are untrusted data, see [Prompts](#prompts).

## Deployment

The installer deploys the MCP server with `install_ai_server` (default `true`, see
[install-advanced.md](../../installer/install-advanced.md)): `roles/ai` publishes `FWO.Ai.Mcp` on the middleware host and
runs it as the service `fworch-ai-mcp` (`roles/ai/templates/fworch-ai-mcp.service.j2`,
`roles/ai/tasks/install_and_run_ai_mcp_service.yml`).

- It listens on `ai_mcp_native_uri` (loopback, port `ai_mcp_internal_port`, default 8890) and the Apache vhost of the
  middleware publishes the MCP endpoint under the same path (`ai_mcp_path`, `/mcp`), passing every event on at once:
  `https://<middleware host>:8888/mcp`. Keep `ai_mcp_path` in step with `McpServerSetup.kMcpPath`.
- The AI harness on the same host calls it at `ai_mcp_native_mcp_uri`.
- Its memory is capped at `fworch_ai_mcp_memory_max_percent` (default 20 %) of the host's RAM, see
  [Systemd units](../installer/systemd-units.md).
- An uninstallation stops the service and removes its unit and files. Setting `install_ai_server=no` on an existing
  installation only removes `/mcp` from the Apache vhost.

## Tests

The unit tests are in `roles/tests-unit/files/FWO.Ai.Tests`. `McpTestServer` runs the MCP server pipeline on a test
server with a key pair of its own, and creates tokens like the middleware does:

- `McpServerAuthenticationTest`: accepted and refused tokens, and the tools by role.
- `McpRateLimitingTest`: the request budget, the concurrent requests, the partition per user and the body size limit.
- `McpReverseProxyTest`: the path Apache publishes and the forwarded headers.
- `McpUserToolsTest`: `get_current_user` without an authenticated request, and the read-only annotation.
- `McpRequestApplicationPromptsTest`: the justification prompts, directly and through the MCP endpoint, including
  escaping, length limits, the answer format, and the language of the answer with the refusal of invalid languages.
- `ClientConformanceTest`: that the prompt name, arguments, kinds, limits and language pattern of the AI client are
  the ones of the MCP server.
- `McpAgentPromptsTest`: the agent instructions prompt, directly and through the MCP endpoint, and that the harness asks
  for it by its name.

The integration test `roles/tests-integration/tasks/test-ai-mcp.yml` checks the installed service: its loopback
listener, the hidden JWT signing key, that it refuses requests without a token (401, directly and through Apache) and
of a user without an FWO role (403), and that the admin gets all tools, directly and through Apache.
