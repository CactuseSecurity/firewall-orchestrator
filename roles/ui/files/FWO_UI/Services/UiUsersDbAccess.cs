using FWO.Logging;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using FWO.ApiClient;
using FWO.Api.Data;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.ApiClient.Queries;

namespace FWO.Ui.Services
{
    public class UiUsersDbAccess
    {
        public UiUser UiUser { get; set; }

        public UiUsersDbAccess(AuthenticationState authState, APIConnection apiConnection)
        {
            ClaimsPrincipal user = authState.User;
            string userDn = user.FindFirstValue("x-hasura-uuid");

            //UiUser = apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn }).Result?[0]; // SHORTER
			
			Log.WriteDebug("Get User Data", $"userDn: {userDn}");
            UiUser[] uiUsers = (Task.Run(() => apiConnection.SendQueryAsync<UiUser[]>(FWO.ApiClient.Queries.AuthQueries.getUserByDn, new { dn = userDn }))).Result;
            if(uiUsers != null && uiUsers.Length > 0)
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
            catch (Exception)
            {
                // maybe admin has deleted uiuser inbetween
            }
        }
    }
}
