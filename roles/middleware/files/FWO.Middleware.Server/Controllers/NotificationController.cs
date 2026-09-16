using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides trusted notification-log operations for UI notification delivery.
/// </summary>
[Authorize]
[ApiController]
[Route("api/notification")]
public class NotificationController(ApiConnection apiConnection) : ControllerBase
{
    /// <summary>
    /// Inserts a notification log entry using the middleware server's service connection.
    /// </summary>
    [HttpPost("log")]
    [Authorize(Roles = Roles.Modeller)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<int>> InsertLog([FromBody] NotificationLogInsertEntry entry)
    {
        try
        {
            return Ok(await NotificationLogHelper.InsertAsync(apiConnection, entry));
        }
        catch (Exception exception)
        {
            Log.WriteError("Notification Log", "Could not insert notification log entry.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Updates a notification log entry using the middleware server's service connection.
    /// </summary>
    [HttpPatch("log")]
    [Authorize(Roles = Roles.Modeller)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<bool>> UpdateLog([FromBody] NotificationLogUpdateParameters parameters)
    {
        try
        {
            await NotificationLogHelper.UpdateAsync(apiConnection, parameters.Id, parameters.Status, parameters.Error);
            return Ok(true);
        }
        catch (Exception exception)
        {
            Log.WriteError("Notification Log", "Could not update notification log entry.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
