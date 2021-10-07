using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FWO.Middleware.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly List<Ldap> ldaps;

        public TenantController(List<Ldap> ldaps, APIConnection apiConnection)
        {
            this.ldaps = ldaps;
        }

        public class AddDeleteTenantParameters
        {
            // [DefaultValue("HelloWorld")]
            public string Dn { get; set; } // = "HelloWorld"
        }

        // POST api/<TenantController>
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<bool> Post([FromBody] AddDeleteTenantParameters tenant)
        {
            bool tenantAdded = false;
            string tenantDn = tenant.Dn;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to add tenant in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        tenantAdded = currentLdap.AddTenant(tenantDn);
                        if (tenantAdded) Log.WriteAudit("AddTenant", $"Tenant {tenantDn} successfully added to {currentLdap.Host()}");
                    });
                }
            }

            // Return status and result
            return tenantAdded;
        }

        // DELETE api/<TenantController>/5
        [HttpDelete]
        [Authorize(Roles = "admin")]
        public async Task<bool> Delete([FromBody] AddDeleteTenantParameters tenant)
        {
            bool tenantDeleted = false;
            string tenantDn = tenant.Dn;

            foreach (Ldap currentLdap in ldaps)
            {
                // Try to delete tenant in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        tenantDeleted = currentLdap.DeleteTenant(tenantDn);
                        if (tenantDeleted) Log.WriteAudit("DeleteTenant", $"Tenant {tenantDn} deleted from {currentLdap.Host()}");
                    });
                }
            }

            // Return status and result
            return tenantDeleted;
        }
    }
}
