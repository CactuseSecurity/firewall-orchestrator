using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class AddTenantRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AddTenantRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "TenantName" from Type string
            string tenantName = GetRequestParameter<string>("TenantName", notNull: true);

            bool tenantAdded = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                // if current Ldap is internal: Try to add tenant in current Ldap
                if (currentLdap.IsInternal())
                {
                    await Task.Run(() =>
                    {
                        tenantAdded = currentLdap.AddTenant(tenantName);
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("tenantAdded", tenantAdded.ToString()));
        }
    }
}
