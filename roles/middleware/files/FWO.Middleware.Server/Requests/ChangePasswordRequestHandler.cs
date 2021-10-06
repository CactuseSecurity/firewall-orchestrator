using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class ChangePasswordRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public ChangePasswordRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "UserDn", "oldPassword", "newPassword" from Type string
            string userDn = GetRequestParameter<string>("UserDn", notNull: true);
            string oldPassword = GetRequestParameter<string>("oldPassword", notNull: true);
            string newPassword = GetRequestParameter<string>("newPassword", notNull: true);

            string errorMsg = "";

            foreach (Ldap currentLdap in Ldaps)
            {
                // if current Ldap is writable: Try to change password in current Ldap
                if (currentLdap.IsWritable())
                {
                    await Task.Run(async () =>
                    {
                        errorMsg = currentLdap.ChangePassword(userDn, oldPassword, newPassword);
                        if (errorMsg == "")
                        {
                            await UiUserHandler.UpdateUserPasswordChanged(ApiConn, userDn);
                        }
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("errorMsg", errorMsg));
        }
    }
}
