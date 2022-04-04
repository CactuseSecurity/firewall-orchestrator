using System;
using System.Net;
using System.Threading.Tasks;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Middleware.RequestParameters;
using RestSharp;

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
                    UserChangePasswordParameters parameters = new UserChangePasswordParameters { LdapId = userConfig.User.LdapConnection.Id, NewPassword = newPassword1, OldPassword = oldPassword, UserId = userConfig.User.DbId };
                    RestResponse<string> middlewareServerResponse = await middlewareClient.ChangePassword(parameters);
                    if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == null)
                    {
                        errorMsg = "internal error";
                    }
                    else
                    {
                        errorMsg = middlewareServerResponse.Data;
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
            else if(!PasswordPolicy.CheckPolicy(newPassword1, userConfig, out errorMsg))
            {
                return false;
            }
            return true;
        }
    }
}
