using FWO.ApiClient;
using FWO.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class DeleteGroupRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public DeleteGroupRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "GroupName" from Type string
            string groupDn = GetRequestParameter<string>("GroupName", notNull: true);

            bool groupDeleted = false;

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to delete group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        groupDeleted = currentLdap.DeleteGroup(groupDn);
                        if (groupDeleted) Log.WriteAudit("DeleteGroup", $"Group {groupDn} deleted from {currentLdap.Host()}");                        
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("groupDeleted", groupDeleted));
        }
    }
}
