using FWO.Logging;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using FWO.ApiClient;
using FWO.Ui.Data.API;
using Microsoft.AspNetCore.Components.Authorization;

namespace FWO.Ui.Services
{
    public class UiUsersDbAccess
    {
        public string Language { get; set; }
        int actUserId = 0;

        public async Task getLanguage(AuthenticationState authState, APIConnection apiConnection)
        {
            Language = "";
            ClaimsPrincipal user = authState.User;
            string userDn ="";
            foreach(var claim in user.Claims)
            {
                if (claim.Type == "x-hasura-uuid")
                {
                    userDn = claim.Value;
                    break;
                }
            }
            Log.WriteDebug("Get User Language", $"userDn: {userDn}");
            UiUser[] uiUser = (await Task.Run(() => apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUserByDn, new { dn = userDn })));
            if(uiUser != null && uiUser.Length > 0)
            {
                Language = uiUser[0].Language;
                actUserId = uiUser[0].DbId;
            }
        }

        public async Task ChangeLanguage(string language, APIConnection apiConnection)
        {
            try
            {
                var Variables = new
                {
                    id = actUserId,
                    language = language
                };
                await Task.Run(() => apiConnection.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUser, Variables));
            }
            catch(Exception)
            {
                // maybe admin has deleted uiuser inbetween
            }
        }
    }
}
