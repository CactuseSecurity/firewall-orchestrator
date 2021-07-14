using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class UpdateUserRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public UpdateUserRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Ldap", "Username", "Email" from Type string
            string ldap = GetRequestParameter<string>("Ldap", notNull: true);
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string email = GetRequestParameter<string>("Email", notNull: false);

            bool userUpdated = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                if (currentLdap.Host() == ldap && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        // Try to update user to current Ldap
                        userUpdated = currentLdap.UpdateUser(userDn, email);
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userUpdated", userUpdated));
        }
    }
}
