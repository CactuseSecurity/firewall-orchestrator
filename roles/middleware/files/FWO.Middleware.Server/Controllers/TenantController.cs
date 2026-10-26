using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for tenant api
	/// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Constructor needing ldap list, connection and the global configuration holding the maximum page size
        /// </summary>
        public TenantController(List<Ldap> ldaps, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        // GET: api/<TenantController>
        /// <summary>
        /// Get all tenants
        /// </summary>
        /// <remarks>
        /// Results are ordered by tenant id, so <c>limit</c> and <c>offset</c> page them deterministically. Without a limit a
        /// page of at most the configured <c>maxRestResponseObjectLimit</c> tenants (default 10000) is returned, and the
        /// <c>X-Has-More</c> response header tells whether further tenants follow.
        /// The query parameters <c>limit</c> and <c>offset</c> select the page.
        /// </remarks>
        /// <param name="paging">Optional query parameters <c>limit</c> and <c>offset</c>.</param>
        /// <returns>List of tenants</returns>
        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TenantGetReturnParameters>>> Get([FromQuery] PagingParameters paging)
        {
            int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
            if (!ListPaging.TryValidate(paging, maxLimit, out ActionResult? pagingError))
            {
                return pagingError!;
            }

            Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenants);
            IEnumerable<TenantGetReturnParameters> orderedTenants = tenants
                .OrderBy(tenant => tenant.Id)
                .Select(tenant => tenant.ToApiParams());
            return ListPaging.TakePage(HttpContext, orderedTenants, paging, maxLimit);
        }

        // POST api/<TenantController>
        /// <summary>
        /// Add tenant to internal Ldap
        /// </summary>
        /// <remarks>
        /// Name (required) &#xA;
        /// Comment (optional) &#xA;
        /// Project (optional) &#xA;
        /// ViewAllDevices (required) &#xA;
        /// </remarks>
        /// <param name="tenant">TenantAddParameters</param>
        /// <returns>Id of new tenant, 0 if no tenant could be created</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<int> Post([FromBody] TenantAddParameters tenant)
        {
            bool tenantAdded = false;
            int tenantId = 0;
            string tenantName = tenant.Name;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add tenant in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable())
                {
                    await Task.Run(async () =>
                    {
                        if (await currentLdap.AddTenant(tenantName))
                        {
                            tenantAdded = true;
                            Log.WriteAudit("AddTenant", $"Tenant {tenantName} successfully added to {currentLdap.Host()}");
                        }
                    });
                }
            }

            if (tenantAdded)
            {
                // Add also to local database table
                try
                {
                    var Variables = new
                    {
                        name = tenant.Name,
                        project = tenant.Project,
                        comment = tenant.Comment,
                        viewAllDevices = tenant.ViewAllDevices,
                        create = DateTime.Now
                    };
                    ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(AuthQueries.addTenant, Variables)).ReturnIds;
                    if (returnIds != null)
                    {
                        tenantId = returnIds[0].NewId;
                        Log.WriteDebug("AddTenant", $"Tenant {tenant.Name} added in database");
                    }
                }
                catch (Exception exception)
                {
                    tenantId = 0;
                    Log.WriteAudit("AddTenant", $"Adding Tenant {tenantName} locally failed: {exception.Message}");
                }
            }

            // Return status and result
            return tenantId;
        }

        // PUT api/<TenantController>/5
        /// <summary>
        /// Update tenant in internal Ldap
        /// </summary>
        /// <remarks>
        /// Id (required) &#xA;
        /// Comment (optional) &#xA;
        /// Project (optional) &#xA;
        /// ViewAllDevices (required) &#xA;
        /// </remarks>
        /// <param name="parameters">TenantEditParameters</param>
        /// <returns>true if updated</returns>
        [HttpPut]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Change([FromBody] TenantEditParameters parameters)
        {
            bool tenantUpdated = false;

            // Try to update tenant in local db
            try
            {
                var Variables = new
                {
                    id = parameters.Id,
                    project = parameters.Project,
                    comment = parameters.Comment,
                    viewAllDevices = parameters.ViewAllDevices
                };
                ReturnId returnId = await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateTenant, Variables);
                if (returnId.UpdatedId == parameters.Id)
                {
                    tenantUpdated = true;
                    Log.WriteDebug("UpdateTenant", $"Tenant {parameters.Id} updated in database");
                }
            }
            catch (Exception exception)
            {
                Log.WriteAudit("UpdateTenant", $"Updating Tenant Id: {parameters.Id} locally failed: {exception.Message}");
            }
            return tenantUpdated;
        }

        // DELETE api/<TenantController>/5
        /// <summary>
        /// Delete tenant from internal Ldap
        /// </summary>
        /// <remarks>
        /// Id (required) &#xA;
        /// Name (required) &#xA;
        /// </remarks>
        /// <param name="tenant">TenantDeleteParameters</param>
        /// <returns>true if tenant deleted</returns>
        [HttpDelete]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Delete([FromBody] TenantDeleteParameters tenant)
        {
            bool tenantDeleted = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete tenant in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable())
                {
                    await Task.Run(async () =>
                    {
                        if (await currentLdap.DeleteTenant(tenant.Name))
                        {
                            Log.WriteAudit("DeleteTenant", $"Tenant {tenant.Name} deleted from {currentLdap.Host()}");
                        }
                    });
                }
            }

            try
            {
                // Delete also from local database table
                var Variables = new { id = tenant.Id };
                int delId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.deleteTenant, Variables)).DeletedId;
                if (delId == tenant.Id)
                {
                    tenantDeleted = true;
                    Log.WriteDebug("DeleteTenant", $"Tenant {tenant.Name} deleted from database");
                }
            }
            catch (Exception exception)
            {
                Log.WriteAudit("DeleteTenant", $"Deleting Tenant {tenant.Id} locally failed: {exception.Message}");
            }

            // Return status and result
            return tenantDeleted;
        }
    }
}
