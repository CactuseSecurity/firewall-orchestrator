using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Config.File;
using FWO.Api.Data;

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
        public static async Task<UiUser> HandleUiUserAtLogin(UiUser user, string jwtToken)
        {
            APIConnection apiConn = new APIConnection(new ConfigFile().ApiServerUri, jwtToken);
            bool userSetInDb = false;
            try
            {
                UiUser[] existingUserFound = await apiConn.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn });

                if (existingUserFound.Length == 1)
                {
                    user.DbId = existingUserFound[0].DbId;
                    user.PasswordMustBeChanged = await UpdateLastLogin(apiConn, user.DbId);
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
                await AddUiUserToDb(apiConn, user);
            }
            return user;
        }

        private static async Task AddUiUserToDb(APIConnection apiConn, UiUser user)
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
                    passwordMustBeChanged = false,
                    ldapConnectionId = user.LdapConnection.Id
                };
                ReturnId[]? returnIds = (await apiConn.SendQueryAsync<NewReturning>(AuthQueries.addUser, Variables)).ReturnIds;
                if(returnIds != null)
                {
                    user.DbId = returnIds[0].NewId;
                }
            }
            catch (Exception exeption)
            {
                Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", exeption);
            }
        }

        private static async Task<bool> UpdateLastLogin(APIConnection apiConn, int id)
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

        public static async Task UpdateUserPasswordChanged(APIConnection apiConn, string userDn, bool passwordMustBeChanged = false)
        {
            try
            {
                var Variables = new
                {
                    dn = userDn, 
                    passwordMustBeChanged = passwordMustBeChanged,
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
