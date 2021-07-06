using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class GetUsersRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public GetUsersRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            string ldap = GetRequestParameter<string>("Ldap", notNull: true);
            string searchPattern = GetRequestParameter<string>("SearchPattern", notNull: true);

            List<KeyValuePair<string, string>> allUsers = new List<KeyValuePair<string, string>>();

            foreach (Ldap currentLdap in Ldaps)
            {
                if (currentLdap.Address == ldap)
                {
                    await Task.Run(() =>
                    {
                        // Get all users from current Ldap
                        allUsers = currentLdap.GetAllUsers(searchPattern);
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("allUsers", allUsers));
        }
    }
}
