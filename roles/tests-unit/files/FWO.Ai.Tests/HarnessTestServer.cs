using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Ai.Tests;

/// <summary>
/// Runs the harness server pipeline on a test server, with a scripted chat client instead of an AI provider and the
/// MCP server pipeline of another test server, and sends AG-UI runs to it.
/// </summary>
internal static class HarnessTestServer
{
    internal const string kRunStarted = "RUN_STARTED";
    internal const string kRunFinished = "RUN_FINISHED";
    internal const string kRunError = "RUN_ERROR";
    internal const string kTextMessageContent = "TEXT_MESSAGE_CONTENT";
    internal const string kToolCallStart = "TOOL_CALL_START";
    internal const string kMessagesSnapshot = "MESSAGES_SNAPSHOT";

    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private const string kEventDataPrefix = "data:";
    private const string kRunIdPrefix = "run-";
    private const string kMessageIdPrefix = "message-";
    private const string kUserRole = "user";

    /// <summary>
    /// Starts the harness server pipeline on a test server, validating tokens with the test public key. Its agent
    /// answers with the given chat client and calls the MCP server of the given test server.
    /// </summary>
    /// <param name="mcpApp">Test server running the MCP server pipeline</param>
    /// <param name="chatClient">Chat client standing in for the AI provider</param>
    /// <param name="settings">Settings of the harness server; the defaults with the MCP endpoint of the test server if null</param>
    internal static async Task<WebApplication> Start(WebApplication mcpApp, IChatClient chatClient, HarnessSettings? settings = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return await BuildAndStart(builder, CreateDefaultSettings(settings), new AgentFactoryHolder(HarnessTestSetup.CreateFactory(mcpApp, chatClient)));
    }

    /// <summary>
    /// Starts the harness server pipeline on Kestrel at a free loopback port, for limits the test server does not
    /// enforce (such as the request body size). Its address is returned by <see cref="McpTestServer.GetBaseAddress"/>.
    /// </summary>
    internal static async Task<WebApplication> StartOnLoopback(WebApplication mcpApp, IChatClient chatClient)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
        return await BuildAndStart(builder, CreateDefaultSettings(null), new AgentFactoryHolder(HarnessTestSetup.CreateFactory(mcpApp, chatClient)));
    }

    /// <summary>
    /// Starts the harness server pipeline on Kestrel at a free loopback port with the given settings and holder of the
    /// agent factory, e.g. an empty one as Program starts with. Its address is returned by
    /// <see cref="McpTestServer.GetBaseAddress"/>.
    /// </summary>
    internal static async Task<WebApplication> StartOnLoopback(HarnessSettings settings, AgentFactoryHolder agentFactories)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
        return await BuildAndStart(builder, settings, agentFactories);
    }

    /// <summary>
    /// Sends an AG-UI run with the given body, optionally with a bearer token.
    /// </summary>
    internal static async Task<HttpResponseMessage> PostRun(HttpClient httpClient, string? accessToken, string body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, HarnessServerSetup.kAgUiPath);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (accessToken != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    /// <summary>
    /// Sends an AG-UI run on the given thread with the given user messages and returns its events.
    /// </summary>
    /// <exception cref="HttpRequestException">The run was not accepted.</exception>
    internal static async Task<List<JsonElement>> RunAsync(HttpClient httpClient, string accessToken, string threadId, params string[] userTexts)
    {
        using HttpResponseMessage response = await PostRun(httpClient, accessToken, CreateRunBody(threadId, userTexts));
        response.EnsureSuccessStatusCode();
        return await ReadEventsAsync(response);
    }

    /// <summary>
    /// Creates the body of an AG-UI run on the given thread with the given user messages.
    /// </summary>
    internal static string CreateRunBody(string threadId, params string[] userTexts)
    {
        return CreateRunInput(threadId, userTexts).ToJsonString();
    }

    /// <summary>
    /// Creates the AG-UI input of a run on the given thread with the given user messages, to be extended by a test.
    /// </summary>
    internal static JsonObject CreateRunInput(string threadId, params string[] userTexts)
    {
        JsonArray messages = [];
        for (int index = 0; index < userTexts.Length; index++)
        {
            messages.Add(new JsonObject { ["id"] = kMessageIdPrefix + index, ["role"] = kUserRole, ["content"] = userTexts[index] });
        }
        return new JsonObject { ["threadId"] = threadId, ["runId"] = kRunIdPrefix + Guid.NewGuid().ToString("N"), ["messages"] = messages };
    }

    /// <summary>
    /// Reads the server-sent events of an AG-UI response.
    /// </summary>
    internal static async Task<List<JsonElement>> ReadEventsAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return body.Split('\n')
            .Where(line => line.StartsWith(kEventDataPrefix, StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[kEventDataPrefix.Length..]).RootElement.Clone())
            .ToList();
    }

    /// <summary>
    /// Returns the types of the events, in order.
    /// </summary>
    internal static List<string> GetEventTypes(IEnumerable<JsonElement> events)
    {
        return events.Select(aguiEvent => aguiEvent.GetProperty("type").GetString() ?? "").ToList();
    }

    /// <summary>
    /// Returns the text the events of a run stream as message content.
    /// </summary>
    internal static string GetText(IEnumerable<JsonElement> events)
    {
        return string.Concat(events
            .Where(aguiEvent => aguiEvent.GetProperty("type").GetString() == kTextMessageContent)
            .Select(aguiEvent => aguiEvent.GetProperty("delta").GetString()));
    }

    /// <summary>
    /// Returns the messages of the single snapshot of the conversation among the events.
    /// </summary>
    internal static List<JsonElement> GetSnapshotMessages(IEnumerable<JsonElement> events)
    {
        JsonElement snapshot = events.Single(aguiEvent => aguiEvent.GetProperty("type").GetString() == kMessagesSnapshot);
        return snapshot.GetProperty("messages").EnumerateArray().ToList();
    }

    /// <summary>
    /// Returns the texts of the messages of the snapshot of the conversation, of each message holding a text.
    /// </summary>
    internal static List<string> GetSnapshotTexts(IEnumerable<JsonElement> events)
    {
        return GetSnapshotMessages(events)
            .Where(message => message.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String)
            .Select(message => message.GetProperty("content").GetString() ?? "")
            .ToList();
    }

    /// <summary>
    /// Returns the roles of the messages of the snapshot of the conversation, in order.
    /// </summary>
    internal static List<string> GetSnapshotRoles(IEnumerable<JsonElement> events)
    {
        return GetSnapshotMessages(events).Select(message => message.GetProperty("role").GetString() ?? "").ToList();
    }

    /// <summary>
    /// Returns the given settings, or the defaults with the MCP endpoint of the test server if null.
    /// </summary>
    private static HarnessSettings CreateDefaultSettings(HarnessSettings? settings)
    {
        return settings ?? new HarnessSettings { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint };
    }

    /// <summary>
    /// Adds the services of the harness server pipeline and starts it.
    /// </summary>
    private static async Task<WebApplication> BuildAndStart(WebApplicationBuilder builder, HarnessSettings settings,
        AgentFactoryHolder agentFactories)
    {
        HarnessServerSetup.AddFwoJwtAuthentication(builder.Services, McpTestServer.PublicKey);
        HarnessServerSetup.AddFwoAgentServer(builder.Services, settings, agentFactories);

        WebApplication app = builder.Build();
        HarnessServerSetup.MapFwoAgent(app);
        await app.StartAsync();
        return app;
    }
}
