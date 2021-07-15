using FWO.ApiClient;
using FWO.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class UpdateGroupRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public UpdateGroupRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "OldName", "NewName" from Type string
            string oldName = GetRequestParameter<string>("OldName", notNull: true);
            string newName = GetRequestParameter<string>("NewName", notNull: true);

            string groupUpdated = "";

            foreach (Ldap currentLdap in Ldaps)
            {
                // Try to update group in current Ldap
                if (currentLdap.IsInternal() && currentLdap.IsWritable() && currentLdap.HasGroupHandling())
                {
                    await Task.Run(() =>
                    {
                        groupUpdated = currentLdap.UpdateGroup(oldName, newName);
                        if (groupUpdated != "") Log.WriteAudit("UpdateGroup", $"Group {oldName} updated to {newName} in {currentLdap.Host()}");
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("groupUpdated", groupUpdated));
        }
    }
}
