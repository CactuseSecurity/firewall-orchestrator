using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using System.Data;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Authentication token generation. Token is of type JSON web token (JWT).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationTokenController : ControllerBase
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Constructor needing jwt writer, ldap list and connection
        /// </summary>
        public AuthenticationTokenController(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Generates a new access and refresh token pair for a user based on the provided authentication parameters.
        /// </summary>
        /// <remarks>This endpoint is typically used during user login to obtain tokens for subsequent
        /// authenticated requests. The access token is stored in the database as a hash for security purposes. Ensure
        /// that the credentials provided are valid to receive a token pair.</remarks>
        /// <param name="parameters">The authentication parameters containing the user's credentials. Must include a valid username and password.
        /// Cannot be null.</param>
        /// <returns>An <see cref="ActionResult{TokenPair}"/> containing the generated access and refresh tokens if
        /// authentication is successful; otherwise, a bad request result with an error message.</returns>
        [HttpPost("GetTokenPair")]
        public async Task<ActionResult<TokenPair>> GetTokenPairAsync([FromBody] AuthenticationTokenGetParameters parameters)
        {
            try
            {
                UiUser? user = null;

                if (parameters != null)
                {
                    string? username = parameters.Username;
                    string? password = parameters.Password;

                    if (username != null && password != null)
                        user = new UiUser { Name = username, Password = password };
                }

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection);

                await authManager.AuthorizeUserAsync(user, validatePassword: true);

                // Creates access and refresh token and stores the access token hash in DB
                TokenPair tokenPair = await authManager.CreateTokenPair(user);

                return Ok(tokenPair);
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Generation", "Error generating token pair", ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Generates a new token pair for a specified user, using administrator credentials for authorization.
        /// </summary>
        /// <remarks>This endpoint is restricted to users with the admin role. The administrator's
        /// credentials are validated before generating a token pair for the target user. The target user's password is
        /// not required for this operation.</remarks>
        /// <param name="parameters">The parameters containing administrator credentials and the target user's information. Must include valid
        /// admin username and password, as well as the target user's name or distinguished name.</param>
        /// <returns>An <see cref="ActionResult{TokenPair}"/> containing the generated token pair for the target user if the
        /// operation succeeds; otherwise, a bad request result with an error message.</returns>
        /// <exception cref="AuthenticationException">Thrown if the provided administrator credentials do not correspond to a user with the admin role.</exception>
        [HttpPost("GetTokenPairForUser")]
        public async Task<ActionResult<TokenPair>> GetTokenPairForUser([FromBody] AuthenticationTokenGetForUserParameters parameters)
        {
            try
            {
                AuthManager authManager = new(jwtWriter, ldaps, apiConnection);
                UiUser adminUser = new() { Name = parameters.AdminUsername, Password = parameters.AdminPassword };

                await authManager.AuthorizeUserAsync(adminUser, validatePassword: true);

                if (!adminUser.Roles.Contains(Roles.Admin))
                {
                    throw new AuthenticationException("Provided credentials do not belong to a user with role admin.");
                }

                UiUser targetUser = new() { Name = parameters.TargetUserName, Dn = parameters.TargetUserDn };

                await authManager.AuthorizeUserAsync(targetUser, validatePassword: false, parameters.Lifetime);

                TokenPair tokenPair = await authManager.CreateTokenPair(targetUser, parameters.Lifetime);

                return Ok(tokenPair);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Refreshes an access token using a valid refresh token.
        /// </summary>
        /// <param name="request">Refresh token request</param>
        /// <returns>New token pair if refresh token is valid</returns>
        [HttpPost("Refresh")]
        public async Task<ActionResult<TokenPair>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest("Refresh token is required");
                }

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection);

                // Validate refresh token
                RefreshTokenInfo? tokenInfo = await authManager.ValidateRefreshToken(request.RefreshToken);

                if (tokenInfo == null)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                UiUser[] users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = tokenInfo.UserId });
                UiUser? user = users.FirstOrDefault();

                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Revoke the old refresh token (token rotation for security)
                await authManager.RevokeRefreshToken(request.RefreshToken);

                // Create new token pair
                TokenPair newTokens = await authManager.CreateTokenPair(user);

                Log.WriteInfo("Token Refresh", $"Successfully refreshed tokens for user {user.Name}");
                return Ok(newTokens);
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Failed to refresh token", ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Revokes a refresh token, preventing it from being used for future token refreshes.
        /// </summary>
        /// <param name="request">The request containing the refresh token to revoke.</param>
        /// <returns>
        /// An <see cref="ActionResult"/> indicating success if the token is revoked;
        /// otherwise, a bad request or unauthorized result with an error message.
        /// </returns>
        [HttpPost("Revoke")]
        public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest("Refresh token is required");
                }

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection);

                RefreshTokenInfo? tokenInfo = await authManager.ValidateRefreshToken(request.RefreshToken);

                if (tokenInfo == null)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                await authManager.RevokeRefreshToken(request.RefreshToken);

                Log.WriteInfo("Token Refresh", $"Successfully revoked refresh token");

                return Ok();
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Failed to refresh token", ex);
                return BadRequest(ex.Message);
            }
        }
    }

    class AuthManager
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private readonly string UserAuthentication = "User Authentication";

        public AuthManager(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Validates user credentials and retrieves user information. Returns a jwt containing it.
        /// </summary>
        /// <param name="user">User to validate. Must contain username / dn and password if <paramref name="validatePassword"/> == true.</param>
        /// <param name="validatePassword">Check password if true.</param>
        /// <param name="lifetime">Set the lifetime of the jwt (optional)</param>
        /// <returns>Jwt, User infos (dn, email, groups, roles, tenant), if credentials are valid.</returns>
        public async Task<string> AuthorizeUserAsync(UiUser? user, bool validatePassword, TimeSpan? lifetime = null)
        {
            // Case: anonymous user
            if (user == null)
                return await jwtWriter.CreateJWT();

            // Retrieve ldap entry for user (throws exception if credentials are invalid)
            (LdapEntry ldapUser, Ldap ldap) = await AuthenticateInAnyLdap(user, validatePassword);

            // Get dn of user
            user.Dn = ldapUser.Dn;

            // Get email of user
            user.Email = Ldap.GetEmail(ldapUser);
            user.Firstname = Ldap.GetFirstName(ldapUser);
            user.Lastname = Ldap.GetLastName(ldapUser);

            // Get groups of user
            user.Groups = await GetGroups(ldapUser, ldap);
            Log.WriteDebug("Get Groups", $"Found groups for user: {string.Join("; ", user.Groups)}");

            // Get roles of user
            user.Roles = await GetRoles(user);

            // Get tenant of user
            user.Tenant = await GetTenantAsync(ldapUser, ldap);
            Log.WriteDebug("Get Tenants", $"Found tenant for user: {user.Tenant?.Name ?? ""}");

            // Remember the hosting ldap
            user.LdapConnection.Id = ldap.Id;

            // Create JWT for validated user with roles and tenant
            return await jwtWriter.CreateJWT(user, lifetime);
        }

        public async Task<List<string>> GetGroups(LdapEntry ldapUser, Ldap ldap)
        {
            List<string> userGroups = ldap.GetGroups(ldapUser);
            if (!ldap.IsInternal())
            {
                object groupsLock = new();
                List<Task> ldapRoleRequests = [];

                foreach (Ldap currentLdap in ldaps.Where(l => l.IsInternal()))
                {
                    ldapRoleRequests.Add(Task.Run(async () =>
                    {
                        // Get groups from current Ldap
                        List<string> currentGroups = await currentLdap.GetGroups([ldapUser.Dn]);
                        lock (groupsLock)
                        {
                            currentGroups = Array.ConvertAll(currentGroups.ToArray(), x => "cn=" + x + "," + currentLdap.GroupSearchPath).ToList();
                            userGroups.AddRange(currentGroups);
                        }
                    }));
                }
                await Task.WhenAll(ldapRoleRequests);
            }
            return userGroups;
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
            LdapEntry? ldapEntry = null;
            Ldap? ldap = null;
            List<Task> ldapValidationRequests = [];
            object dnLock = new();
            bool ldapFound = false;

            foreach (Ldap currentLdap in ldaps.Where(x => x.Active))
            {
                ldapValidationRequests.Add(Task.Run(async () =>
                {
                    Log.WriteDebug(UserAuthentication, $"Trying to authenticate {user.Name + " " + user.Dn} against LDAP {currentLdap.Address}:{currentLdap.Port} ...");

                    LdapEntry? currentLdapEntry = await TryLogin(currentLdap, user, validatePassword);
                    if (currentLdapEntry != null)
                    {
                        lock (dnLock)
                        {
                            if (!ldapFound)
                            {
                                ldapEntry = currentLdapEntry;
                                ldap = currentLdap;
                                ldapFound = true;
                            }
                        }
                    }
                }));
            }

            while (ldapValidationRequests.Count > 0)
            {
                Task finishedDnRequest = await Task.WhenAny(ldapValidationRequests);

                if (ldapEntry != null && ldap != null)
                {
                    return (ldapEntry, ldap);
                }
                ldapValidationRequests.Remove(finishedDnRequest);
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

            foreach (Ldap currentLdap in ldaps.Where(l => l.HasRoleHandling()))
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
                Log.WriteError("Token Validation", "Error validating refresh token", ex);
                return null;
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
        /// Revokes a refresh token by marking it as revoked
        /// </summary>
        public async Task RevokeRefreshToken(string refreshToken)
        {
            try
            {
                string tokenHash = GenerateTokenHash(refreshToken);

                var mutationVariables = new
                {
                    tokenHash = tokenHash,
                    revokedAt = DateTime.UtcNow
                };

                await apiConnection.SendQueryAsync<object>(AuthQueries.revokeRefreshToken, mutationVariables);
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
        private static string GenerateTokenHash(string token)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Create access and refresh token pair for given user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="accessTokenLifetime"></param>
        /// <returns></returns>
        public async Task<TokenPair> CreateTokenPair(UiUser? user = null, TimeSpan? accessTokenLifetime = null)
        {
            UiUserHandler uiUserHandler = new(jwtWriter.CreateJWTMiddlewareServer());

            TimeSpan accessLifetime = accessTokenLifetime ?? TimeSpan.FromHours(await uiUserHandler.GetExpirationTime(nameof(ConfigData.AccessTokenLifetimeHours)));
            string accessToken = await jwtWriter.CreateJWT(user, accessLifetime);

            string refreshToken = JwtWriter.GenerateRefreshToken();
            int refreshTokenLifetimeDays = await uiUserHandler.GetExpirationTime(nameof(ConfigData.RefreshTokenLifetimeDays));
            DateTime refreshExpiry = DateTime.UtcNow.AddDays(refreshTokenLifetimeDays);

            await StoreRefreshToken(user?.DbId ?? 0, refreshToken, refreshExpiry);

            return new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpires = DateTime.UtcNow.Add(accessLifetime),
                RefreshTokenExpires = refreshExpiry
            };
        }
    }
}
