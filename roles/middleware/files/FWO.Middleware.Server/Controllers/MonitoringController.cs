using FWO.Basics;
using FWO.Services.SystemUsage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Exposes resource-usage snapshots of the middleware server for the UI monitoring page.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController(ISystemUsageSnapshotProvider systemUsageProvider) : ControllerBase
    {
        /// <summary>
        /// Returns the current system and middleware-process resource usage snapshot.
        /// </summary>
        /// <returns>The current resource usage snapshot.</returns>
        [HttpGet("SystemUsage")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Auditor}")]
        public ActionResult<SystemUsageSnapshot> GetSystemUsage()
        {
            return Ok(systemUsageProvider.Collect());
        }
    }
}
