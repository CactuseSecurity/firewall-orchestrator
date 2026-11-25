using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Decides which tools the model may call without asking the user. Fail-closed: only a tool the MCP server marks as
/// read-only runs right away; for every other tool the agent returns a <see cref="ToolApprovalRequestContent"/> to the
/// client, which answers it in the next run (AG-UI: an interrupt of the run, answered by a resume entry).
/// </summary>
internal static class ToolApproval
{
    /// <summary>
    /// Returns a new list of the tools in which every function, except an MCP tool marked read-only, requires approval.
    /// Functions that already require approval and tools that are not functions are taken over as they are.
    /// </summary>
    /// <param name="tools">Tools of the MCP server</param>
    /// <exception cref="ArgumentNullException">The tools or one of them are null.</exception>
    internal static List<AITool> RequireApprovalUnlessReadOnly(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return tools.Select(ApplyApprovalPolicy).ToList();
    }

    /// <summary>
    /// Wraps a function in an <see cref="ApprovalRequiredAIFunction"/> unless it is read-only or already requires approval.
    /// </summary>
    private static AITool ApplyApprovalPolicy(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (tool is not AIFunction function || IsReadOnlyMcpTool(function) || RequiresApproval(function))
        {
            return tool;
        }
        return new ApprovalRequiredAIFunction(function);
    }

    /// <summary>
    /// Tells whether the function is an MCP tool the MCP server annotates as read-only. The hint is taken from the
    /// tool itself only, not from a function wrapping it, as a wrapper may do more than the tool.
    /// </summary>
    private static bool IsReadOnlyMcpTool(AIFunction function)
    {
        return function is McpClientTool mcpTool && mcpTool.ProtocolTool.Annotations?.ReadOnlyHint == true;
    }

    /// <summary>
    /// Tells whether the function already requires approval, detected the same way the function invocation of
    /// Microsoft.Extensions.AI detects it.
    /// </summary>
    private static bool RequiresApproval(AIFunction function)
    {
        return function.GetService<ApprovalRequiredAIFunction>() != null;
    }
}
