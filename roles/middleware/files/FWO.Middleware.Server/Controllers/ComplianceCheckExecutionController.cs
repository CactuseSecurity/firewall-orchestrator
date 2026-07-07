using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Controller class for manually executed compliance checks.
/// </summary>
[Authorize]
[ApiController]
[Route("api/Compliance")]
public class ComplianceCheckExecutionController(ApiConnection apiConnection) : ControllerBase
{
    /// <summary>
    /// Runs an initial compliance check.
    /// </summary>
    /// <returns>True when the check completed successfully.</returns>
    [HttpGet("ComplianceCheck")]
    [Authorize(Roles = $"{Roles.Admin}")]
    public async Task<bool> InitialComplianceCheck()
    {
        try
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            await complianceCheck.RunComplianceCheck(ComplianceCheckType.Variable);
            await complianceCheck.PersistDataAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
