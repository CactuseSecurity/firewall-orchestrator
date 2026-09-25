using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Authenticates users against the connected LDAPs and issues, validates and revokes their tokens.
    /// </summary>
    class AuthManager
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private readonly string UserAuthentication = "User Authentication";
        private const string kValidationLogCategory = "Token Validation";

        public AuthManager(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection, TokenLifetimeProvider? tokenLifetimeProvider = null)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
            this.tokenLifetimeProvider = tokenLifetimeProvider ?? new TokenLifetimeProvider();
        }

        /// <summary>
        /// Validates user credentials and retrieves a fully populated UI user context.
        /// </summary>
        /// <param name="user">User to validate. Must contain username or dn and password if <paramref name="validatePassword"/> is true. If null, no authentication is performed and null is returned.</param>
        /// <param name="validatePassword">True to validate the user's password during authentication.</param>
        /// <param name="updateLoginState">True to persist login-related local UI-user updates such as last-login timestamps and first-time creation.</param>
        /// <returns>An authenticated user including dn, groups, roles, tenant, db id, and ownerships, or null for anonymous access.</returns>
        public async Task<UiUser?> AuthenticateAndBuildUserAsync(UiUser? user, bool validatePassword, bool updateLoginState = true)
        {
            // Case: anonymous user
            if (user == null)
            {
                return null;
            }

            // Retrieve ldap entry for user (throws exception if credentials are invalid)
            (LdapEntry ldapUser, Ldap ldap) = await AuthenticateInAnyLdap(user, validatePassword);

            // Get dn of user
            user.Dn = ldapUser.Dn;
            Log.WriteInfo(UserAuthentication, $"User {user.Name} authenticated with dn={user.Dn}, selected_ldap=({AuthLoggingHelper.FormatSelectedLdap(ldap)})");

            // Get email of user
            user.Email = Ldap.GetEmail(ldapUser);
            user.Firstname = Ldap.GetFirstName(ldapUser);
            user.Lastname = Ldap.GetLastName(ldapUser);

            // Get groups of user
            user.Groups = await GetGroups(ldapUser, ldap);
            Log.WriteInfo(UserAuthentication, $"Resolved groups for user dn={user.Dn}: {AuthLoggingHelper.FormatResolvedGroups(user.Groups)}");

            // Get roles of user
            user.Roles = await GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(ldapUser, ldap);
            Log.WriteDebug("Get Tenants", $"Found tenant for user: {user.Tenant?.Name ?? ""}");

            // Remember the hosting ldap
            user.LdapConnection.Id = ldap.Id;

            return await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: updateLoginState, createIfMissing: updateLoginState);
        }

        /// <summary>
        /// Validates the user, builds the login context, and returns a signed JWT.
        /// </summary>
        /// <param name="user">User to validate. Must contain username or dn and password if <paramref name="validatePassword"/> is true. If null, an anonymous JWT is returned.</param>
        /// <param name="validatePassword">True to validate the user's password during authentication.</param>
        /// <param name="lifetime">Optional JWT lifetime override.</param>
        /// <returns>A signed JWT for the authenticated user or an anonymous JWT if <paramref name="user"/> is null.</returns>
        public async Task<string> AuthorizeUserAsync(UiUser? user, bool validatePassword, TimeSpan? lifetime = null)
        {
            UiUser? authenticatedUser = await AuthenticateAndBuildUserAsync(user, validatePassword);
            if (authenticatedUser == null)
            {
                return jwtWriter.CreateJWT(null, tokenLifetimeProvider.GetAnonymousTokenLifetime());
            }

            TimeSpan accessLifetime = lifetime ?? await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);
            return jwtWriter.CreateJWT(authenticatedUser, accessLifetime);
        }

        /// <summary>
        /// Resolves the ldap group memberships of the given user.
        /// </summary>
        /// <param name="ldapUser">Ldap entry of the user.</param>
        /// <param name="ldap">Ldap connection hosting the user.</param>
        /// <returns>Distinct list of group dns the user belongs to.</returns>
        public async Task<List<string>> GetGroups(LdapEntry ldapUser, Ldap ldap)
        {
            return await new UserGroupResolver(ldaps).GetGroups(ldapUser, ldap);
        }

        public async Task<(LdapEntry, Ldap)> AuthenticateInAnyLdap(UiUser user, bool validatePassword)
        {
            Log.WriteDebug(UserAuthentication, $"Trying to get ldap entry for user: {user.Name + " " + user.Dn}...");

            if (user.Dn == "" && user.Name == "")
            {
                throw new AuthenticationException("A0001 Invalid credentials. Username / User DN must not be empty.");
            }
            else
            {
                (LdapEntry? ldapEntry, Ldap? ldap) = await TryLoginAnywhere(user, validatePassword);
                if (ldapEntry != null && ldap != null)
                {
                    return (ldapEntry, ldap);
                }
                Log.WriteInfo(UserAuthentication, $"User {user.Name} not found in any connected LDAP.");
            }

            // Invalid User Credentials
            throw new AuthenticationException("A0002 Invalid credentials");
        }

        private async Task<(LdapEntry?, Ldap?)> TryLoginAnywhere(UiUser user, bool validatePassword)
        {
            List<Ldap> activeLdaps = ldaps.Where(x => x.Active).ToList();
            if (activeLdaps.Count == 0)
            {
                return (null, null);
            }

            (LdapEntry? Entry, Ldap? Ldap)[] ldapResults = new (LdapEntry?, Ldap?)[activeLdaps.Count];
            List<Task> ldapValidationRequests = [];

            for (int ldapIndex = 0; ldapIndex < activeLdaps.Count; ldapIndex++)
            {
                int currentIndex = ldapIndex;
                Ldap currentLdap = activeLdaps[currentIndex];
                ldapValidationRequests.Add(Task.Run(async () =>
                {
                    Log.WriteDebug(UserAuthentication, $"Trying to authenticate {user.Name + " " + user.Dn} against LDAP {currentLdap.Address}:{currentLdap.Port} ...");
                    LdapEntry? currentLdapEntry = await TryLogin(currentLdap, user, validatePassword);
                    ldapResults[currentIndex] = (currentLdapEntry, currentLdapEntry != null ? currentLdap : null);
                }));
            }

            await Task.WhenAll(ldapValidationRequests);

            int preferredLdapIndex = AuthLdapSelection.GetPreferredLdapIndex(
                ldapResults.Select(result => result.Entry != null).ToList());
            if (preferredLdapIndex >= 0)
            {
                return ldapResults[preferredLdapIndex];
            }
            return (null, null);
        }

        private async Task<LdapEntry?> TryLogin(Ldap currentLdap, UiUser user, bool validatePassword)
        {
            LdapEntry? currentLdapEntry = null;
            try
            {
                currentLdapEntry = await currentLdap.GetLdapEntry(user, validatePassword);
                if (currentLdapEntry != null)
                {
                    // User was successfully authenticated via this LDAP
                    if (user.Name == Roles.Importer)
                    {
                        Log.WriteDebug(UserAuthentication, $"User {user.Name + " " + currentLdapEntry.Dn} found.");
                    }
                    else
                    {
                        Log.WriteInfo(UserAuthentication, $"User {user.Name + " " + currentLdapEntry.Dn} found.");
                    }
                }
            }
            catch
            {
                // this Ldap can't validate user, but maybe another one can
            }
            return currentLdapEntry;
        }

        public async Task<List<string>> GetRoles(UiUser user)
        {
            List<string> dnList =
            [
                user.Dn,
                .. user.Groups, // search all groups where user is member for group associated roles
            ];

            List<string> userRoles = [];
            object rolesLock = new();

            List<Task> ldapRoleRequests = [];

            // inactive connections must not contribute roles: the injected ldap list is a startup snapshot
            // that still contains deactivated connections, and login itself only binds against active ones
            foreach (Ldap currentLdap in ldaps.Where(l => l.Active && l.HasRoleHandling()))
            {
                // if current Ldap has roles stored
                ldapRoleRequests.Add(Task.Run(async () =>
                {
                    // Get roles from current Ldap
                    List<string> currentRoles = await currentLdap.GetRoles(dnList);

                    lock (rolesLock)
                    {
                        userRoles.AddRange(currentRoles);
                    }
                }));
            }

            await Task.WhenAll(ldapRoleRequests);

            // If no roles found
            if (userRoles.Count == 0)
            {
                // Use anonymous role
                Log.WriteWarning("Missing roles", $"No roles for user \"{user.Dn}\" could be found. Using anonymous role.");
                userRoles.Add(Roles.Anonymous);
            }

            return userRoles;
        }

        public async Task<Tenant?> GetTenantAsync(LdapEntry user, Ldap ldap)
        {
            Tenant tenant = new();
            if (ldap.TenantId != null)
            {
                Log.WriteDebug("Get Tenant", $"This LDAP has the fixed tenant {ldap.TenantId.Value}");
                tenant.Id = ldap.TenantId.Value;
            }
            else
            {
                tenant.Name = new DistName(user.Dn).GetTenantNameViaLdapTenantLevel(ldap.TenantLevel);
                if (tenant.Name == "")
                {
                    return null;
                }
                Log.WriteDebug("Get Tenant", $"extracting TenantName as: {tenant.Name} from {user.Dn}");
                if (tenant.Name == ldap.GlobalTenantName)
                {
                    tenant.Id = GlobalConst.kTenant0Id;
                }
                else
                {
                    var tenNameObj = new { tenant_name = tenant.Name };
                    Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, tenNameObj, "getTenantId");
                    if (tenants.Length > 0)
                    {
                        tenant.Id = tenants[0].Id;
                    }
                    else
                    {
                        // tenant unknown: create in db. This should only happen for users from external Ldaps
                        // no further search for devices etc necessary
                        return await CreateTenantInDb(tenant);
                    }
                }
            }
            await AddDevices(apiConnection, tenant);

            return tenant;
        }

        private async Task<Tenant?> CreateTenantInDb(Tenant tenant)
        {
            try
            {
                var Variables = new
                {
                    name = tenant.Name,
                    project = "",
                    comment = "",
                    viewAllDevices = false,
                    create = DateTime.Now
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(AuthQueries.addTenant, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    tenant.Id = returnIds[0].NewId;
                    return tenant;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("AddTenant", $"Adding Tenant {tenant.Name} locally failed: {exception.Message}");
                return null;
            }
        }

        // the following method adds device visibility information to a tenant (fetched from API)
        private static async Task AddDevices(ApiConnection conn, Tenant tenant)
        {
            var tenIdObj = new { tenantId = tenant.Id };

            Device[] deviceIds = await conn.SendQueryAsync<Device[]>(AuthQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            tenant.VisibleGatewayIds = Array.ConvertAll(deviceIds, device => device.Id);

            Management[] managementIds = await conn.SendQueryAsync<Management[]>(AuthQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            tenant.VisibleManagementIds = Array.ConvertAll(managementIds, management => management.Id);
        }

        /// <summary>
        /// Validates a refresh token and returns token info if valid
        /// </summary>
        public async Task<RefreshTokenInfo?> ValidateRefreshToken(string refreshToken)
        {
            try
            {
                string tokenHash = GenerateTokenHash(refreshToken);

                var queryVariables = new
                {
                    tokenHash = tokenHash,
                    currentTime = DateTime.UtcNow
                };

                RefreshTokenInfo[] result = await apiConnection.SendQueryAsync<RefreshTokenInfo[]>(AuthQueries.getRefreshToken, queryVariables);

                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                // Nothing is swallowed here, so null keeps a single meaning: the query
                // succeeded and matched no live token. Returning null for a failed query
                // instead made the caller answer "invalid or expired refresh token" - and a
                // client that believes that discards a refresh token which is perfectly
                // good, so any API fault, from an outage to a Hasura permission or schema
                // error, would end every session. The caller decides the status.
                Log.WriteError(kValidationLogCategory, "Error validating refresh token", ex);
                throw;
            }
        }

        /// <summary>
        /// Stores a refresh token in the database
        /// </summary>
        public async Task StoreRefreshToken(int userId, string refreshToken, DateTime expiresAt)
        {
            try
            {
                string tokenHash = GenerateTokenHash(refreshToken);

                var mutationVariables = new
                {
                    userId = userId,
                    tokenHash = tokenHash,
                    expiresAt = expiresAt,
                    createdAt = DateTime.UtcNow
                };

                await apiConnection.SendQueryAsync<object>(AuthQueries.storeRefreshToken, mutationVariables);
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Storage", "Error storing refresh token", ex);
                throw;
            }
        }

        /// <summary>
        /// Revokes a refresh token by marking it as revoked.
        /// </summary>
        /// <param name="refreshToken">The refresh token to revoke.</param>
        /// <returns>The number of refresh-token rows that were revoked.</returns>
        public async Task<int> RevokeRefreshToken(string refreshToken)
        {
            try
            {
                string tokenHash = GenerateTokenHash(refreshToken);

                var mutationVariables = new
                {
                    tokenHash = tokenHash,
                    revokedAt = DateTime.UtcNow
                };

                ReturnId revokeResult = await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.revokeRefreshToken, mutationVariables);
                return revokeResult.AffectedRows;
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Revocation", "Error revoking refresh token", ex);
                throw;
            }
        }

        /// <summary>
        /// Generates a SHA256 hash of the refresh token for secure storage
        /// </summary>
        internal static string GenerateTokenHash(string token)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Creates an access-token and refresh-token pair for the given user.
        /// </summary>
        /// <param name="user">The authenticated user for whom the token pair is created. If null, an anonymous access token without a refresh token is created.</param>
        /// <param name="accessTokenLifetime">Optional access-token lifetime override.</param>
        /// <param name="issueRefreshToken">When false, no refresh token is issued so the access token cannot be rotated into a longer-lived session. Used for delegated (admin-on-behalf-of-user) tokens.</param>
        /// <returns>A token pair containing the signed access token and, for authenticated users, a persisted refresh token with its expiration metadata.</returns>
        public async Task<TokenPair> CreateTokenPair(UiUser? user = null, TimeSpan? accessTokenLifetime = null, bool issueRefreshToken = true)
        {
            TimeSpan accessLifetime = user == null
                ? tokenLifetimeProvider.GetAnonymousTokenLifetime()
                : accessTokenLifetime ?? await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);

            string accessToken = jwtWriter.CreateJWT(user, accessLifetime);

            JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            string refreshToken = "";
            DateTime refreshExpiry = DateTime.MinValue;

            if (user is not null && issueRefreshToken)
            {
                refreshToken = JwtWriter.GenerateRefreshToken();
                TimeSpan refreshLifetime = await tokenLifetimeProvider.GetRefreshTokenLifetimeAsync(apiConnection);
                refreshExpiry = DateTime.UtcNow.Add(refreshLifetime);
                await StoreRefreshToken(user.DbId, refreshToken, refreshExpiry);
            }

            return new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpires = jwt.ValidTo,
                RefreshTokenExpires = refreshExpiry
            };
        }
    }
}
