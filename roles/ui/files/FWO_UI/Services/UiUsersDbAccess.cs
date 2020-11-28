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
        public UiUser UiUser { get; set; }

        public UiUsersDbAccess (AuthenticationState authState, APIConnection apiConnection)
        {
            ClaimsPrincipal user = authState.User;
            string userDn = user.FindFirstValue("x-hasura-uuid");

            Log.WriteDebug("Get User Language", $"userDn: {userDn}");

            UiUser[] uiUser = await apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUserByDn, new { dn = userDn });
            if(uiUser != null && uiUser.Length > 0)
            {
                UiUser = uiUsers[0];
            }
        }

        public async Task ChangeLanguage(string language, APIConnection apiConnection)
        {
            try
            {
                var Variables = new
                {
                    id = UiUser.DbId,
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
