using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class GetGroupsRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public GetGroupsRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            string ldap = GetRequestParameter<string>("Ldap", notNull: true);
            string searchPattern = GetRequestParameter<string>("SearchPattern", notNull: true);

            List<string> allGroups = new List<string>();

            foreach (Ldap currentLdap in Ldaps)
            {
                if (currentLdap.Host() == ldap)
                {
                    await Task.Run(() =>
                    {
                        // Get all groups from current Ldap
                        allGroups = currentLdap.GetAllGroups(searchPattern);
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("allGroups", allGroups));
        }
    }
}
