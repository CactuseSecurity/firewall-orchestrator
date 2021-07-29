using System;
using System.Net;
using System.Threading.Tasks;
using FWO.ApiConfig;
using FWO.Middleware.Client;

namespace FWO.Ui.Services
{
    public class PasswordChanger
    {
        private string errorMsg = "";
        private MiddlewareClient middlewareClient;

        public PasswordChanger(MiddlewareClient MiddlewareClient)
        {
            middlewareClient = MiddlewareClient;
        }

        public async Task<string> ChangePassword(string oldPassword, string newPassword1, string newPassword2, UserConfig userConfig)
        {
            try
            {
                if(doChecks(oldPassword, newPassword1, newPassword2, userConfig))
                {
                    // Ldap call
                    MiddlewareServerResponse middlewareServerResponse = await middlewareClient.ChangePassword(userConfig.User.Dn, oldPassword, newPassword1, userConfig.User.Jwt);
                    if (middlewareServerResponse.Status != HttpStatusCode.OK)
                    {
                        errorMsg = "internal error";
                    }
                    else
                    {
                        errorMsg = middlewareServerResponse.GetResult<string>("errorMsg");
                    }
                }
            }
            catch (Exception exception)
            {
                errorMsg = exception.Message;
            }
            return errorMsg;
        }

        private bool doChecks(string oldPassword, string newPassword1, string newPassword2, UserConfig userConfig)
        {
            if(oldPassword == "")
            {
                errorMsg = userConfig.GetText("E5401");
                return false;
            }
            else if(newPassword1 == "")
            {
                errorMsg = userConfig.GetText("E5402");
                return false;
            }
            else if(newPassword1 == oldPassword)
            {
                errorMsg = userConfig.GetText("E5403");
                return false;
            }
            else if(newPassword1 != newPassword2)
            {
                errorMsg = userConfig.GetText("E5404");
                return false;
            }
            else if(!new PasswordPolicy().checkPolicy(newPassword1, userConfig, out errorMsg))
            {
                return false;
            }
            return true;
        }
    }
}
