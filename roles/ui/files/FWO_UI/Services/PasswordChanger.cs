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
                    MiddlewareServerResponse apiAuthResponse = await middlewareClient.ChangePassword(userConfig.User.Dn, oldPassword, newPassword1, userConfig.User.Jwt);
                    if (apiAuthResponse.Status == HttpStatusCode.BadRequest)
                    {
                        errorMsg = "internal error";
                    }
                    else
                    {
                        errorMsg = apiAuthResponse.GetResult<string>("errorMsg");
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
                errorMsg = "please insert the old password";
                return false;
            }
            else if(newPassword1 == "")
            {
                errorMsg = "please insert a new password";
                return false;
            }
            else if(newPassword1 == oldPassword)
            {
                errorMsg = "new password must differ from old one";
                return false;
            }
            else if(newPassword1 != newPassword2)
            {
                errorMsg = "please insert the same new password twice";
                return false;
            }
            else if(!((new PasswordPolicy()).checkPolicy(newPassword1, userConfig, out errorMsg)))
            {
                return false;
            }
            return true;
        }
    }
}
