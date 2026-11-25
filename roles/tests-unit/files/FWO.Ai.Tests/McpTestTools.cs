using FWO.Basics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace FWO.Ai.Tests;

/// <summary>
/// Records the values stored by <see cref="McpTestWriteTools"/>, so that a test can tell whether the tool ran.
/// </summary>
internal sealed class McpTestToolCalls
{
    private readonly ConcurrentQueue<string> storedValues = new();

    /// <summary>
    /// Values stored by the tool, in the order of the calls.
    /// </summary>
    internal List<string> StoredValues => storedValues.ToList();

    /// <summary>
    /// Records a call of the tool.
    /// </summary>
    internal void Record(string value)
    {
        storedValues.Enqueue(value);
    }
}

/// <summary>
/// Test-only MCP tool standing in for a tool that changes data: it is not marked read-only, so the harness must not call
/// it before the user approved the call. Registered by <see cref="Register"/> on a test server only.
/// </summary>
[Authorize]
internal class McpTestWriteTools(McpTestToolCalls toolCalls)
{
    internal const string kStoreValueTool = "store_test_value";
    internal const string kValueArgument = "value";
    internal const string kStoredPrefix = "stored ";

    /// <summary>
    /// Registers the tool on the MCP server of a test server, recording its calls in the given record.
    /// </summary>
    internal static void Register(IMcpServerBuilder mcpServer, McpTestToolCalls toolCalls)
    {
        mcpServer.Services.AddSingleton(toolCalls);
        mcpServer.WithTools<McpTestWriteTools>();
    }

    /// <summary>
    /// Records the value and confirms it.
    /// </summary>
    [McpServerTool(Name = kStoreValueTool)]
    [Description("Stores a test value.")]
    public string StoreTestValue([Description("Value to store")] string value)
    {
        toolCalls.Record(value);
        return kStoredPrefix + value;
    }
}

/// <summary>
/// Test-only MCP tool restricted to admins and auditors, offered by every test server (see <see cref="McpTestServer"/>),
/// so that tests of the role filter do not depend on a production tool.
/// </summary>
[Authorize]
internal class McpTestRoleRestrictedTools
{
    internal const string kRoleRestrictedTool = "get_role_restricted_test_value";
    internal const string kRoleRestrictedValue = "role restricted test value";

    /// <summary>
    /// Returns a fixed value; only offered to callers holding one of the listed roles.
    /// </summary>
    [McpServerTool(Name = kRoleRestrictedTool, ReadOnly = true)]
    [Description("Returns a test value, only for admins and auditors.")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    public static string GetRoleRestrictedTestValue()
    {
        return kRoleRestrictedValue;
    }
}

/// <summary>
/// Test-only MCP prompts the production server does not offer.
/// </summary>
[Authorize]
internal class McpTestPrompts
{
    internal const string kEmptyPrompt = "empty_test_prompt";

    /// <summary>
    /// Returns a prompt without messages.
    /// </summary>
    [McpServerPrompt(Name = kEmptyPrompt)]
    [Description("Prompt without messages.")]
    public static GetPromptResult GetEmptyPrompt()
    {
        return new GetPromptResult { Messages = [] };
    }
}
