using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Middleware.RequestParameters;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly List<Ldap> ldaps;
        private readonly APIConnection apiConnection;

        public TenantController(List<Ldap> ldaps, APIConnection apiConnection)
        {
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        // GET: api/<TenantController>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<List<TenantGetParameters>> Get()
        {
            Tenant[] tenants = (await apiConnection.SendQueryAsync<Tenant[]>(FWO.ApiClient.Queries.AuthQueries.getTenants));
            List<TenantGetParameters> tenantList = new List<TenantGetParameters>();
            foreach (Tenant tenant in tenants)
            {
                tenantList.Add(tenant.ToApiParams());
            }
            return tenantList;
        }

        // POST api/<TenantController>
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
                        tenantAdded = currentLdap.AddTenant(tenantName);
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
                        superAdmin = tenant.Superadmin,
                        create = DateTime.Now
                    };
                    ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.ApiClient.Queries.AuthQueries.addTenant, Variables)).ReturnIds;
                    if (returnIds != null)
                    {
                        tenantId = returnIds[0].NewId;
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

        // DELETE api/<TenantController>/5
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
                            tenantDeleted = true;
                            Log.WriteAudit("DeleteTenant", $"Tenant {tenant.Name} deleted from {currentLdap.Host()}");
                        }
                    });
                }
            }

            if (tenantDeleted) 
            {
                try
                {
                    // Delete also from local database table
                    var Variables = new { id = tenant.Id };
                    int delId = (await apiConnection.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.deleteTenant, Variables)).DeletedId;
                }
                catch (Exception exception)
                {
                    Log.WriteAudit("AddTenant", $"Deleting Tenant {tenant.Id} locally failed: {exception.Message}");
                    tenantDeleted = false;
                }
            }

            // Return status and result
            return tenantDeleted;
        }
    }
}
