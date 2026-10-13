using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Data.Workflow;
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
        /// <param name="bindDirectoryObject">True to bind a local user without directory object id to the one of the user, see <see cref="UiUserDirectoryObjectBinding"/>.
        /// Only a password login may bind.</param>
        /// <returns>The given user enriched with local database id, password-change flag, and ownership information.</returns>
        /// <exception cref="InvalidOperationException">The user has no LDAP connection, the workflow visibility groups could not be determined, or a new local user could not be added.</exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">The local user of the dn is bound to another directory entry.</exception>
        /// <exception cref="Exception">Rethrown unchanged when the API could not be reached or the local user could not be looked up.</exception>
        public static async Task<UiUser> SynchronizeUiUserContext(ApiConnection apiConnection, UiUser user, bool updateLastLogin = true, bool createIfMissing = true,
            bool bindDirectoryObject = false)
        {
            // A dn is unique only inside its directory: the same dn in two LDAP connections belongs to
            // two different subjects, so the local user must never be resolved by the dn alone (SEC-11).
            int ldapConnectionId = user.LdapConnection?.Id ?? 0;
            if (ldapConnectionId <= 0)
            {
                throw new InvalidOperationException($"User {user.Name} with dn {user.Dn} has no LDAP connection, so its local user cannot be determined.");
            }

            // The local user is resolved outside the error handling below: a failed lookup must refuse the request
            // instead of continuing as if the user were new, which would hand out the local user stored for the dn
            // without the directory object check (GHSA-695m-f6mv-9g72).
            UiUser? existingUser = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, user, ldapConnectionId, bindDirectoryObject);
            bool userSetInDb = existingUser != null;
            if (existingUser != null)
            {
                user.DbId = existingUser.DbId;
                user.PasswordMustBeChanged = updateLastLogin
                    ? await UpdateLastLogin(apiConnection, user.DbId)
                    : existingUser.PasswordMustBeChanged;
            }
            else
            {
                Log.WriteDebug("User not found", $"Couldn't find {user.Name} in internal database");
            }

            bool workflowVisibilityGroupsLoaded = false;
            bool workflowVisibilityGroupsAttempted = false;
            try
            {
                await GetOwnershipsFromOwnerLdap(apiConnection, user);
                workflowVisibilityGroupsAttempted = true;
                workflowVisibilityGroupsLoaded = await GetWorkflowVisibilityGroupIds(apiConnection, user);
            }
            // An unreachable API must stay recognizable as such, so that callers can answer
            // "retry" instead of reporting the request itself as failed.
            catch (Exception exeption) when (!ApiReachability.IndicatesUnreachableApi(exeption))
            {
                Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", exeption);
            }

            if (workflowVisibilityGroupsAttempted && !workflowVisibilityGroupsLoaded)
            {
                throw new InvalidOperationException($"Workflow visibility groups could not be determined for User {user.Name}.");
            }

            if (!userSetInDb)
            {
                if (!createIfMissing)
                {
                    throw new KeyNotFoundException($"User {user.Name} with dn {user.Dn} could not be found in the local database.");
                }

                Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to internal database.");
                user.DbId = 0;
                await UpsertUiUser(apiConnection, user, true);
                if (user.DbId <= 0)
                {
                    // without a local user the token would carry user id 0, the system user holding the global settings
                    throw new InvalidOperationException($"User {user.Name} with dn {user.Dn} could not be added to the local database.");
                }
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
        /// Resolves workflow visibility group memberships for the given user from the database.
        /// </summary>
        /// <param name="apiConn">API connection used to read the workflow visibility groups.</param>
        /// <param name="user">User whose workflow visibility group ids are resolved.</param>
        /// <returns>True if the visibility groups could be determined, otherwise false.</returns>
        /// <exception cref="Exception">Rethrown unchanged when the API could not be reached.</exception>
        public static async Task<bool> GetWorkflowVisibilityGroupIds(ApiConnection apiConn, UiUser user)
        {
            try
            {
                user.WorkflowVisibilityGroupIds.Clear();

                List<WorkflowVisibilityGroup> visibilityGroups = await apiConn.SendQueryAsync<List<WorkflowVisibilityGroup>>(RequestQueries.getWorkflowVisibilityGroups);
                HashSet<string> userDns = new(DistName.DnComparer)
                {
                    user.Dn
                };
                foreach (string groupDn in user.Groups ?? [])
                {
                    userDns.Add(groupDn);
                }

                Log.WriteDebug("Get workflow visibility groups",
                    $"User {user.Name} candidate dns: {AuthLoggingHelper.FormatResolvedGroups(userDns)}; visibility groups loaded: {visibilityGroups.Count}");

                foreach (WorkflowVisibilityGroup visibilityGroup in visibilityGroups)
                {
                    if (visibilityGroup.Members.Any(member => userDns.Contains(member.MemberDn)))
                    {
                        user.WorkflowVisibilityGroupIds.Add(visibilityGroup.Id);
                        Log.WriteDebug("Get workflow visibility groups",
                            $"User {user.Name} matched workflow visibility group id={visibilityGroup.Id}, name={visibilityGroup.Name}, members={AuthLoggingHelper.FormatResolvedGroups(visibilityGroup.Members.Select(member => member.MemberDn))}");
                    }
                }

                user.WorkflowVisibilityGroupIds = user.WorkflowVisibilityGroupIds.Distinct().ToList();
                Log.WriteDebug("Get workflow visibility groups",
                    $"User {user.Name} resolved workflow visibility group ids: [{string.Join(", ", user.WorkflowVisibilityGroupIds)}]");
                return true;
            }
            catch (Exception exeption)
            {
                Log.WriteError("Get workflow visibility groups", $"Workflow visibility groups could not be determined for User {user.Name}.", exeption);
                if (ApiReachability.IndicatesUnreachableApi(exeption))
                {
                    throw;
                }
                return false;
            }
        }

        /// <summary>
        /// add user to uiuser - either with or without current login time
        /// </summary>
        /// <remarks>
        /// Without a login (e.g. app data import, workflow recipients) a local user bound to another directory entry is
        /// left untouched: the dn was handed to another person, whose data must not overwrite it (GHSA-695m-f6mv-9g72).
        /// The user's DbId then stays unset.
        /// </remarks>
        /// <returns>void</returns> 
        public static async Task UpsertUiUser(ApiConnection apiConn, UiUser user, bool loginHappened = false)
        {
            try
            {
                if (!loginHappened && await IsBoundToAnotherDirectoryEntry(apiConn, user))
                {
                    return;
                }
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
                        directoryObjectId = user.DirectoryObjectId,
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
                        ldapConnectionId = user.LdapConnection.Id,
                        directoryObjectId = user.DirectoryObjectId
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

        private static async Task<bool> IsBoundToAnotherDirectoryEntry(ApiConnection apiConn, UiUser user)
        {
            UiUser? storedUser = (await apiConn.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn,
                new { dn = user.Dn, ldapConnectionId = user.LdapConnection.Id })).FirstOrDefault();
            if (storedUser?.DirectoryObjectId == null || string.Equals(storedUser.DirectoryObjectId, user.DirectoryObjectId, StringComparison.Ordinal))
            {
                return false;
            }
            Log.WriteWarning("Add User", $"Local user id={storedUser.DbId} with dn {user.Dn} is bound to directory object {storedUser.DirectoryObjectId}, " +
                $"but the dn now names {user.DirectoryObjectId ?? "an entry without object id"}. The local user is not updated.");
            return true;
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
        /// <param name="apiConn">API connection used to update the local user.</param>
        /// <param name="userId">Local database id of the user. The dn is not used, as it does not identify a user across LDAP connections.</param>
        /// <param name="passwordMustBeChanged">New value of the flag.</param>
        public static async Task UpdateUserPasswordChanged(ApiConnection apiConn, int userId, bool passwordMustBeChanged = false)
        {
            try
            {
                var Variables = new
                {
                    id = userId,
                    passwordMustBeChanged = passwordMustBeChanged,
                    changeTime = DateTime.UtcNow
                };
                await apiConn.SendQueryAsync<ReturnId>(AuthQueries.updateUserPasswordChange, Variables);
            }
            catch (Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {userId} could not be updated in database.", exeption);
            }
        }
    }
}
