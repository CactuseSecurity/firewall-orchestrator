using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class GetAllRolesRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public GetAllRolesRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // No parameters

            List<KeyValuePair<string, List<string>>> allRoles = new List<KeyValuePair<string, List<string>>>();
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                ldapRoleRequests.Add(Task.Run(() =>
                {
                    // if current Ldap has roles stored
                    if (currentLdap.RoleSearchPath != "")
                    {
                        // Get all roles from current Ldap
                        List<KeyValuePair<string, List<string>>> currentRoles = currentLdap.GetAllRoles();
                        allRoles.AddRange(currentRoles);
                    }
                }));
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("allRoles", allRoles));
        }
    }
}
