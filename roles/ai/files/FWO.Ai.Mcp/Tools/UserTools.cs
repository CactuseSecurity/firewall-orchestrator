using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Claims;

namespace FWO.Ai.Mcp.Tools;

/// <summary>
/// Name and roles of the user an access token was issued for.
/// </summary>
/// <param name="Name">User name (unique_name claim)</param>
/// <param name="Roles">FWO roles (role claims)</param>
internal record CurrentUser(string Name, List<string> Roles);

/// <summary>
/// MCP tools describing the calling user, e.g. to check that a client sends a valid access token.
/// </summary>
[Authorize]
internal class UserTools(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Returns name and roles taken from the validated access token of the current request.
    /// </summary>
    [McpServerTool(ReadOnly = true)]
    [Description("Returns the name and the FWO roles of the user the access token was issued for.")]
    public CurrentUser GetCurrentUser()
    {
        ClaimsIdentity? identity = httpContextAccessor.HttpContext?.User.Identity as ClaimsIdentity;
        List<string> roles = identity?.FindAll(identity.RoleClaimType).Select(claim => claim.Value).ToList() ?? [];
        return new CurrentUser(identity?.Name ?? "", roles);
    }
}
