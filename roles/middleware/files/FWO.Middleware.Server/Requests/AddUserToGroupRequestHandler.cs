using FWO.ApiClient;
using FWO.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class AddUserToGroupRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AddUserToGroupRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username", "Group" from Type string
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string group = GetRequestParameter<string>("Group", notNull: true);

            bool userAdded = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to add user to group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        userAdded = currentLdap.AddUserToEntry(userDn, group);
                        if (userAdded) Log.WriteAudit("AddUserToGroup", $"user {userDn} successfully added to group {group} in {currentLdap.Host()}");                        
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userAdded", userAdded));
        }
    }
}
