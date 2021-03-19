using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class DeleteUserRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public DeleteUserRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username" from Type string
            string userDn = GetRequestParameter<string>("Username", notNull: true);

            bool userDeleted = false;
            List<Task> ldapRoleRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                ldapRoleRequests.Add(Task.Run(() =>
                {
                    // if current Ldap has is internal: Try to delete user in current Ldap
                    if (currentLdap.IsInternal() && currentLdap.DeleteUser(userDn))
                    {
                        userDeleted = true;
                    }
                }));
            }

            await Task.WhenAll(ldapRoleRequests);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userDeleted", userDeleted));
        }
    }
}
