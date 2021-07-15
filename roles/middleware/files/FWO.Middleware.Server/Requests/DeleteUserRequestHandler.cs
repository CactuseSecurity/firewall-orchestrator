using FWO.ApiClient;
using FWO.Logging;
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
            // Get parameters from request. Expected parameters: "Ldap", "Username" from Type string
            string ldap = GetRequestParameter<string>("Ldap", notNull: true);
            string userDn = GetRequestParameter<string>("Username", notNull: true);

            bool userDeleted = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to delete user in current Ldap
                if (currentLdap.Host() == ldap && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        userDeleted = currentLdap.DeleteUser(userDn);
                        if (userDeleted) Log.WriteAudit("DeleteUser", $"User {userDn} deleted from {ldap}");                        
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userDeleted", userDeleted));
        }
    }
}
