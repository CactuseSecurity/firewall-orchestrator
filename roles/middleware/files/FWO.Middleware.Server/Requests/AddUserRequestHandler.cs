using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FWO.Logging;

namespace FWO.Middleware.Server.Requests
{
    class AddUserRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AddUserRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Ldap", "Username", "Password", "Email" from Type string
            string ldap = GetRequestParameter<string>("Ldap", notNull: true);
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string password = GetRequestParameter<string>("Password", notNull: true);
            string email = GetRequestParameter<string>("Email", notNull: false);

            bool userAdded = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to add user to current Ldap
                if (currentLdap.Host() == ldap && currentLdap.IsWritable())
                {
                    await Task.Run(() =>
                    {
                        userAdded = currentLdap.AddUser(userDn, password, email);
                        if (userAdded) Log.WriteAudit("AddUser", $"user {userDn} successfully added to {ldap}");
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("userAdded", userAdded));
        }
    }
}
