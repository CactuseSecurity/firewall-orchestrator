using FWO.ApiClient;
using FWO.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class DeleteTenantRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public DeleteTenantRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "TenantName" from Type string
            string tenantName = GetRequestParameter<string>("TenantName", notNull: true);

            bool tenantDeleted = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                // if current Ldap is internal: Try to delete tenant in current Ldap
                if (currentLdap.IsInternal())
                {
                    await Task.Run(() =>
                    {
                        tenantDeleted = currentLdap.DeleteTenant(tenantName);
                    });
                }
            }

            // Return status and result
            // Log.WriteAudit("DeleteTenant", $"Tenant {tenantName} deleted");
            return WrapResult(HttpStatusCode.OK, ("tenantDeleted", tenantDeleted.ToString()));
        }
    }
}
