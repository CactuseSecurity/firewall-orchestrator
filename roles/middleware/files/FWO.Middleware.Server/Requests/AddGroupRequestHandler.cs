using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Logging;

namespace FWO.Middleware.Server.Requests
{
    class AddGroupRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public AddGroupRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "GroupName" from Type string
            string groupName = GetRequestParameter<string>("GroupName", notNull: true);

            string groupAdded = "";

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to add group to current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        groupAdded = currentLdap.AddGroup(groupName);
                        if (groupAdded != "") Log.WriteAudit("AddGroup", $"group {groupAdded} successfully added to {currentLdap.Host()}");
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("groupAdded", groupAdded));
        }
    }
}
