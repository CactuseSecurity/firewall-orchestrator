using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

namespace FWO.Middleware.Server.Requests
{
    class SetPasswordRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public SetPasswordRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // Get parameters from request. Expected parameters: "Username", "Password" from Type string
            string userDn = GetRequestParameter<string>("Username", notNull: true);
            string password = GetRequestParameter<string>("Password", notNull: true);

            string errorMsg = "";

            foreach (Ldap currentLdap in Ldaps)
            {
                // if current Ldap is internal: Try to update user password in current Ldap
                if (currentLdap.IsWritable())
                {
                    await Task.Run(async () =>
                    {
                        errorMsg = currentLdap.SetPassword(userDn, password);
                        if (errorMsg == "")
                        {
                            bool passwordMustBeChanged = true;
                            List<string> roles = currentLdap.GetRoles(new List<string>(){userDn}).ToList();
                            if(roles.Contains("auditor"))
                            {
                                // the auditor can't be forced to change password as he is not allowed to do it
                                passwordMustBeChanged = false;
                            }
                            await (new UiUserHandler()).updateUserPasswordChanged(ApiConn, userDn, passwordMustBeChanged);
                        }
                    });
                }
            }

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("errorMsg", errorMsg));
        }
    }
}
