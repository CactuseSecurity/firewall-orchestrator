using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Controllers
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

		/// <summary>
		/// Constructor needing ldap list and connection
		/// </summary>
        public TenantController(List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        // GET: api/<TenantController>
        /// <summary>
        /// Get all tenants
        /// </summary>
        /// <returns>List of tenants</returns>
        [HttpGet]
        [Authorize(Roles = "admin, auditor")]
        public async Task<List<TenantGetReturnParameters>> Get()
        {
            Tenant[] tenants = (await apiConnection.SendQueryAsync<Tenant[]>(FWO.Api.Client.Queries.AuthQueries.getTenants));
            List<TenantGetReturnParameters> tenantList = new List<TenantGetReturnParameters>();
            foreach (Tenant tenant in tenants)
            {
                tenantList.Add(tenant.ToApiParams());
            }
            return tenantList;
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
        [Authorize(Roles = "admin")]
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
                    await Task.Run(() =>
                    {
                        if (currentLdap.AddTenant(tenantName))
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
                        // superAdmin = tenant.Superadmin,
                        create = DateTime.Now
                    };
                    ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.AuthQueries.addTenant, Variables)).ReturnIds;
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
        [Authorize(Roles = "admin")]
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
                ReturnId returnId = await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.AuthQueries.updateTenant, Variables);
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
        [Authorize(Roles = "admin")]
        public async Task<bool> Delete([FromBody] TenantDeleteParameters tenant)
        {
            bool tenantDeleted = false;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete tenant in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        if(currentLdap.DeleteTenant(tenant.Name))
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
                int delId = (await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.AuthQueries.deleteTenant, Variables)).DeletedId;
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
