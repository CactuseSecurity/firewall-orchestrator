using FWO.Basics;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FWO.Ai.Mcp.Tools;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class RandomNumberTools
{
    /// <summary>
    /// Generates a random number; only offered to callers holding one of the listed roles.
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    public static int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        return Random.Shared.Next(min, max);
    }
}
