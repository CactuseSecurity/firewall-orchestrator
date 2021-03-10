using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Config;
using System;
using FWO.Api.Data;
using System.Threading.Tasks;

namespace FWO.Middleware.Server
{
    public class UiUserHandler
    {
        /// <summary>
        /// if the user logs in for the first time, user details (excluding password) are written to DB bia API
        /// the database id is retrieved and added to the user 
        /// the user id is needed for allowing access to report_templates
        /// </summary>
        /// <returns> user including its db id </returns>
        public async Task<UiUser> handleUiUserAtLogin(UiUser user, string jwtToken)
        {
            APIConnection apiConn = new APIConnection(new ConfigFile().ApiServerUri, jwtToken);
            bool userSetInDb = false;
            try
            {
                UiUser[] existingUserFound = await apiConn.SendQueryAsync<UiUser[]>(AuthQueries.getUserByUuid, new { uuid = user.Dn });

                if (existingUserFound.Length == 1)
                {
                    user.DbId = existingUserFound[0].DbId;
                    user.PasswordMustBeChanged = await updateLastLogin(apiConn, user.DbId);
                    userSetInDb = true;
                }
                else
                {
                    Log.WriteError("User not found", $"Couldn't find {user.Name} exactly once!");
                }
            }
            catch(Exception exeption)
            {
                Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", exeption);
            }

            if(!userSetInDb)
            {
                Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to database.");
                await addUiUserToDb(apiConn, user);
                user.PasswordMustBeChanged = true;
            }
            return user;
        }

        private async Task addUiUserToDb(APIConnection apiConn, UiUser user)
        {
            try
            {
                // add new user to uiuser
                var Variables = new
                {
                    uuid = user.Dn, 
                    uiuser_username = user.Name,
                    email = user.Email,
                    tenant = (user.Tenant != null ? user.Tenant.Id : (int?)null),
                    loginTime = DateTime.UtcNow,
                    passwordMustBeChanged = false
                };
                user.DbId = (await apiConn.SendQueryAsync<NewReturning>(AuthQueries.addUser, Variables)).ReturnIds[0].NewId;
            }
            catch (Exception exeption)
            {
                Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", exeption);
            }
        }

        private async Task<bool> updateLastLogin(APIConnection apiConn, int id)
        {
            try
            {
                var Variables = new
                {
                    id = id, 
                    loginTime = DateTime.UtcNow
                };
                return (await apiConn.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUserLastLogin, Variables)).PasswordMustBeChanged;
            }
            catch(Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {id} could not be updated in database.", exeption);
            }
            return true;
        }

        public async Task updateUserPasswordChanged(APIConnection apiConn, string userDn)
        {
            try
            {
                var Variables = new
                {
                    dn = userDn, 
                    passwordMustBeChanged = false,
                    changeTime = DateTime.UtcNow
                };
                await apiConn.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUserPasswordChange, Variables);
            }
            catch(Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {userDn} could not be updated in database.", exeption);
            }
        }
    }
}
