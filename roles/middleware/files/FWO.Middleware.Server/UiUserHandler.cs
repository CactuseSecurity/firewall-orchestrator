using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Logging;
using Newtonsoft.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Helper class to read config value for expiration time
    /// </summary>
    public class ConfExpirationTime
    {
        /// <summary>
        /// config value for expiration time
        /// </summary>
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public int ExpirationValue { get; set; }
    }

    /// <summary>
    /// Handler class for local Ui user
    /// </summary>
    /// <remarks>
    /// Constructor needing the jwt token
    /// </remarks>
    public static class UiUserHandler
    {
        /// <summary>
        /// Get the configurated value for the session timeout.
        /// </summary>
        /// <returns>session timeout value in minutes</returns>
        public static async Task<int> GetExpirationTime(ApiConnection apiConnection, string lifetimeKey)
        {
            int expirationTime = GlobalConst.kSessionExpirationTimeDefault;
            PropertyInfo? property = typeof(ConfigData).GetProperty(lifetimeKey);
            string? lifetimeKeyDBName = GetLifetimeKeyDbName(property);
            if (string.IsNullOrEmpty(lifetimeKeyDBName))
            {
                return expirationTime;
            }

            try
            {
                List<ConfExpirationTime> resultList = await apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, new { key = lifetimeKeyDBName });
                if (resultList.Count > 0)
                {
                    return resultList[0].ExpirationValue;
                }

                return GetDefaultExpirationTime(property, lifetimeKey, expirationTime);
            }
            catch (Exception exeption)
            {
                Log.WriteError("Get ExpirationTime Error", $"Error while trying to find config value in database. Taking default value", exeption);
            }

            return expirationTime;
        }

        /// <summary>
        /// Get configured unit for token lifetimes.
        /// </summary>
        public static async Task<TokenLifetimeUnit> GetExpirationUnit(ApiConnection apiConnection, string lifetimeUnitKey)
        {
            TokenLifetimeUnit defaultUnit = TokenLifetimeUnit.Hours;
            PropertyInfo? property = typeof(ConfigData).GetProperty(lifetimeUnitKey);
            string? lifetimeUnitKeyDbName = GetLifetimeKeyDbName(property);
            if (string.IsNullOrEmpty(lifetimeUnitKeyDbName))
            {
                return defaultUnit;
            }

            try
            {
                List<ConfigItem> resultList = await apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, new { key = lifetimeUnitKeyDbName });
                if (resultList.Count > 0 && !string.IsNullOrWhiteSpace(resultList[0].Value))
                {
                    string value = resultList[0].Value!;
                    if (Enum.TryParse(value, true, out TokenLifetimeUnit parsedUnit))
                    {
                        return parsedUnit;
                    }

                    if (int.TryParse(value, out int parsedIndex) && Enum.IsDefined(typeof(TokenLifetimeUnit), parsedIndex))
                    {
                        return (TokenLifetimeUnit)parsedIndex;
                    }
                }

                return GetDefaultExpirationUnit(property, defaultUnit);
            }
            catch (Exception exeption)
            {
                Log.WriteError("Get ExpirationUnit Error", "Error while trying to find config value in database. Taking default value", exeption);
            }

            return GetDefaultExpirationUnit(property, defaultUnit);
        }

        private static string? GetLifetimeKeyDbName(PropertyInfo? property)
        {
            return property?.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
        }

        private static int GetDefaultExpirationTime(PropertyInfo? property, string lifetimeKey, int expirationTime)
        {
            ConfigData defaultConfigValue = new();
            object? propertyValue = property?.GetValue(defaultConfigValue);

            if (propertyValue is int intPropertyValue && intPropertyValue >= 0)
            {
                return intPropertyValue;
            }

            // if no value is set in DB, take the default from config file and if that is not set, take the hardcoded constant
            return lifetimeKey switch
            {
                nameof(ConfigData.AccessTokenLifetime) => defaultConfigValue.AccessTokenLifetime > 0 ? defaultConfigValue.AccessTokenLifetime : expirationTime,
                nameof(ConfigData.RefreshTokenLifetime) => defaultConfigValue.RefreshTokenLifetime > 0 ? defaultConfigValue.RefreshTokenLifetime : expirationTime,
                _ => expirationTime,
            };
        }

        private static TokenLifetimeUnit GetDefaultExpirationUnit(PropertyInfo? property, TokenLifetimeUnit fallbackUnit)
        {
            ConfigData defaultConfigValue = new();
            object? propertyValue = property?.GetValue(defaultConfigValue);
            if (propertyValue is TokenLifetimeUnit tokenLifetimeUnit)
            {
                return tokenLifetimeUnit;
            }

            return fallbackUnit;
        }

        /// <summary>
        /// Loads and synchronizes the local UI-user context needed for JWT claim generation.
        /// </summary>
        /// <param name="apiConnection">API connection used to read and update UI-user metadata.</param>
        /// <param name="user">The authenticated user whose local UI context should be synchronized.</param>
        /// <param name="updateLastLogin">True to update the persisted last-login timestamp.</param>
        /// <param name="createIfMissing">True to create the user in the local database if no record exists yet.</param>
        /// <returns>The given user enriched with local database id, password-change flag, and ownership information.</returns>
        public static async Task<UiUser> SynchronizeUiUserContext(ApiConnection apiConnection, UiUser user, bool updateLastLogin = true, bool createIfMissing = true)
        {
            bool userSetInDb = false;
            try
            {
                UiUser[] existingUsers = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = user.Dn });

                if (existingUsers.Length > 0)
                {
                    user.DbId = existingUsers[0].DbId;
                    user.PasswordMustBeChanged = updateLastLogin
                        ? await UpdateLastLogin(apiConnection, user.DbId)
                        : existingUsers[0].PasswordMustBeChanged;
                    userSetInDb = true;
                }
                else
                {
                    Log.WriteDebug("User not found", $"Couldn't find {user.Name} in internal database");
                }
                await GetOwnershipsFromOwnerLdap(apiConnection, user);
            }
            catch (Exception exeption)
            {
                Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", exeption);
            }

            if (!userSetInDb)
            {
                if (!createIfMissing)
                {
                    throw new KeyNotFoundException($"User {user.Name} with dn {user.Dn} could not be found in the local database.");
                }

                Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to internal database.");
                await UpsertUiUser(apiConnection, user, true);
            }
            return user;
        }

        /// <summary>
        /// if the user logs in for the first time, user details (excluding password) are written to DB bia API
        /// the database id is retrieved and added to the user 
        /// the user id is needed for allowing access to report_templates
        /// </summary>
        /// <returns> user including its db id </returns>
        public static async Task<UiUser> HandleUiUserAtLogin(ApiConnection apiConnection, UiUser user)
        {
            return await SynchronizeUiUserContext(apiConnection, user, updateLastLogin: true, createIfMissing: true);
        }

        /// <summary>
        /// add the ownerships to the given user
        /// </summary>
        public static async Task GetOwnershipsFromOwnerLdap(ApiConnection apiConn, UiUser user)
        {
            try
            {
                user.Ownerships.Clear();
                user.RecertOwnerships.Clear();

                // if the user logging in is the main user for an application, add the ownerships
                List<FwoOwner> directOwnerships = await apiConn.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForUser, new { userDns = new List<string> { user.Dn } });
                foreach (var owner in directOwnerships)
                {
                    user.Ownerships.Add(owner.Id);
                }

                List<string> groupsOfUser = user.Groups ?? [];
                if (groupsOfUser.Count > 0)
                {
                    List<FwoOwner> groupOwnerships = await apiConn.SendQueryAsync<List<FwoOwner>>(
                        OwnerQueries.getOwnersFromGroups,
                        new { groupDns = groupsOfUser });
                    foreach (var owner in groupOwnerships)
                    {
                        user.Ownerships.Add(owner.Id);
                    }
                }

                List<string> ownerDns = [user.Dn];
                ownerDns.AddRange(groupsOfUser);
                ownerDns = ownerDns
                    .Where(dn => !string.IsNullOrWhiteSpace(dn))
                    .Distinct(DistName.DnComparer)
                    .ToList();
                List<FwoOwner> recertOwnerships = ownerDns.Count == 0
                    ? []
                    : await apiConn.SendQueryAsync<List<FwoOwner>>(
                        OwnerQueries.getOwnersForDnsWithRecertification,
                        new { dns = ownerDns });
                user.RecertOwnerships.AddRange(recertOwnerships.Select(owner => owner.Id));

                user.Ownerships = user.Ownerships.Distinct().ToList();
                user.RecertOwnerships = user.RecertOwnerships.Distinct().ToList();
            }
            catch (Exception exeption)
            {
                Log.WriteError("Get ownerships", $"Ownerships could not be detemined for User {user.Name}.", exeption);
            }
        }

        /// <summary>
        /// add user to uiuser - either with or without current login time
        /// </summary>
        /// <returns>void</returns> 
        public static async Task UpsertUiUser(ApiConnection apiConn, UiUser user, bool loginHappened = false)
        {
            try
            {
                // add new user to uiuser
                if (loginHappened)
                {
                    var VariablesWithLogin = new
                    {
                        uuid = user.Dn,
                        uiuser_username = user.Name,
                        uiuser_first_name = user.Firstname,
                        uiuser_last_name = user.Lastname,
                        email = user.Email,
                        tenant = user.Tenant != null ? user.Tenant.Id : (int?)null,
                        passwordMustBeChanged = false,
                        ldapConnectionId = user.LdapConnection.Id,
                        loginTime = DateTime.UtcNow
                    };
                    ReturnId[]? returnIds = (await apiConn.SendQueryAsync<ReturnIdWrapper>(AuthQueries.upsertUiUser, VariablesWithLogin)).ReturnIds;
                    if (returnIds != null)
                    {
                        user.DbId = returnIds[0].NewId;
                    }
                }
                else
                {
                    var VariablesWithoutLogin = new
                    {
                        uuid = user.Dn,
                        uiuser_username = user.Name,
                        uiuser_first_name = user.Firstname,
                        uiuser_last_name = user.Lastname,
                        email = user.Email,
                        tenant = user.Tenant != null ? user.Tenant.Id : (int?)null,
                        passwordMustBeChanged = false,
                        ldapConnectionId = user.LdapConnection.Id
                    };
                    ReturnId[]? returnIds = (await apiConn.SendQueryAsync<ReturnIdWrapper>(AuthQueries.upsertUiUser, VariablesWithoutLogin)).ReturnIds;
                    if (returnIds != null)
                    {
                        user.DbId = returnIds[0].NewId;
                    }
                }
            }
            catch (Exception exeption)
            {
                Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", exeption);
            }
        }

        private static async Task<bool> UpdateLastLogin(ApiConnection apiConn, int id)
        {
            try
            {
                var Variables = new
                {
                    id = id,
                    loginTime = DateTime.UtcNow
                };
                return (await apiConn.SendQueryAsync<ReturnId>(AuthQueries.updateUserLastLogin, Variables)).PasswordMustBeChanged;
            }
            catch (Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {id} could not be updated in database.", exeption);
            }
            return true;
        }

        /// <summary>
        /// Update the passwordMustBeChanged flag.
        /// </summary>
        public static async Task UpdateUserPasswordChanged(ApiConnection apiConn, string userDn, bool passwordMustBeChanged = false)
        {
            try
            {
                var Variables = new
                {
                    dn = userDn,
                    passwordMustBeChanged = passwordMustBeChanged,
                    changeTime = DateTime.UtcNow
                };
                await apiConn.SendQueryAsync<ReturnId>(AuthQueries.updateUserPasswordChange, Variables);
            }
            catch (Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {userDn} could not be updated in database.", exeption);
            }
        }
    }
}
