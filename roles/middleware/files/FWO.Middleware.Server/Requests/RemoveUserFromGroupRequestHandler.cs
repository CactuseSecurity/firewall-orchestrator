using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class RemoveUserFromGroupRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public RemoveUserFromGroupRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username", "Group" from Type string
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string group = GetRequestParameter<string>("Group", notNull: true);

            bool userRemoved = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                // if current Ldap is internal: Try to remove user from group in current Ldap
                if (currentLdap.IsWritable() && currentLdap.GroupSearchPath != null && currentLdap.GroupSearchPath != "")
                {
                    await Task.Run(() =>
                    {
                        userRemoved = currentLdap.RemoveUserFromEntry(userDn, group);
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userRemoved", userRemoved));
        }
    }
}
