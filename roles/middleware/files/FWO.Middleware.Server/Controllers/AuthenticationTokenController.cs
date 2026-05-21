using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
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
    /// Authentication token generation. Token is of type JSON web token (JWT).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationTokenController : ControllerBase
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;

        /// <summary>
        /// Constructor needing jwt writer, ldap list and connection
        /// </summary>
        public AuthenticationTokenController(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection, TokenLifetimeProvider tokenLifetimeProvider)
        {
            this.jwtWriter = jwtWriter;
            this.ldaps = ldaps;
            this.apiConnection = apiConnection;
            this.tokenLifetimeProvider = tokenLifetimeProvider;
        }

        /// <summary>
        /// Generates a new access and refresh token pair for a user based on the provided authentication parameters.
        /// </summary>
        /// <remarks>This endpoint is typically used during user login to obtain tokens for subsequent
        /// authenticated requests. The refresh token is stored in the database as a hash for security purposes. Ensure
        /// that the credentials provided are valid to receive a token pair.</remarks>
        /// <param name="parameters">The authentication parameters containing the user's credentials. Must include a valid username and password.
        /// Cannot be null.</param>
        /// <returns>An <see cref="ActionResult{TokenPair}"/> containing the generated access and refresh tokens if
        /// authentication is successful; otherwise, a bad request result with an error message.</returns>
        [HttpPost("GetTokenPair")]
        public async Task<ActionResult<TokenPair>> GetTokenPair([FromBody] AuthenticationTokenGetParameters parameters)
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

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);

                UiUser? authenticatedUser = await authManager.AuthenticateAndBuildUserAsync(user, validatePassword: true);

                // Creates access and refresh token and stores the refresh token hash in DB
                TokenPair tokenPair = await authManager.CreateTokenPair(authenticatedUser);
                WriteTokenPairAudit("IssueTokenPair", tokenPair, authenticatedUser, authenticatedUser == null
                    ? "Issued anonymous bootstrap token pair."
                    : "Issued token pair after successful authentication.");

                return Ok(tokenPair);
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Generation", "Error generating token pair", ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Generates a new access and refresh token pair for a specified user, using administrator credentials for authorization.
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
                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);
                UiUser adminUser = new() { Name = parameters.AdminUsername, Password = parameters.AdminPassword };

                UiUser authenticatedAdminUser = await authManager.AuthenticateAndBuildUserAsync(adminUser, validatePassword: true)
                    ?? throw new AuthenticationException("Provided admin credentials are invalid.");

                if (!authenticatedAdminUser.Roles.Contains(Roles.Admin))
                {
                    throw new AuthenticationException("Provided credentials do not belong to a user with role admin.");
                }

                UiUser targetUser = new() { Name = parameters.TargetUserName, Dn = parameters.TargetUserDn };

                UiUser authenticatedTargetUser = await authManager.AuthenticateAndBuildUserAsync(targetUser, validatePassword: false)
                    ?? throw new AuthenticationException("Provided target user credentials are invalid.");

                TimeSpan delegatedLifetime = tokenLifetimeProvider.CapDelegatedUserTokenLifetime(parameters.Lifetime);
                TokenPair tokenPair = await authManager.CreateTokenPair(authenticatedTargetUser, delegatedLifetime);
                WriteTokenPairAudit("IssueDelegatedTokenPair", tokenPair, authenticatedAdminUser,
                    $"Issued delegated token pair for target user \"{authenticatedTargetUser.Name}\".");

                return Ok(tokenPair);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Generates an authentication token (jwt) given valid credentials.  
        /// </summary>
        /// <remarks>
        /// Username (required)&#xA;
        /// Password (required)
        /// </remarks>
        /// <param name="parameters">Credentials</param>
        /// <returns>Jwt, if credentials are vaild.</returns>
        [HttpPost("Get")]
        public async Task<ActionResult<string>> GetAsync([FromBody] AuthenticationTokenGetParameters parameters)
        {
            try
            {
                UiUser? user = null;

                if (parameters != null)
                {
                    string? username = parameters.Username;
                    string? password = parameters.Password;

                    // Create User from given parameters / If user does not provide login data => anonymous login
                    if (username != null && password != null)
                        user = new UiUser { Name = username, Password = password };
                }

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);

                UiUser? authenticatedUser = await authManager.AuthenticateAndBuildUserAsync(user, validatePassword: true);
                TimeSpan accessLifetime = authenticatedUser == null
                    ? tokenLifetimeProvider.GetAnonymousTokenLifetime()
                    : await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);
                string jwt = jwtWriter.CreateJWT(authenticatedUser, accessLifetime);
                WriteJwtAudit("IssueAccessToken", jwt, authenticatedUser, authenticatedUser == null
                    ? "Issued anonymous bootstrap access token."
                    : "Issued access token after successful authentication.");

                return Ok(jwt);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Generates an authentication token (jwt) for the specified user given valid admin credentials.  
        /// </summary>
        /// <remarks>
        /// AdminUsername (required) - Example: "admin" &#xA;
        /// AdminPassword (required) - Example: "password" &#xA;
        /// Lifetime (optional) - Example: "365.12:02:00" ("days.hours:minutes:seconds") &#xA;
        /// TargetUserDn OR TargetUserName (required) - Example: "uid=demo_user,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal" OR "demo_user" 
        /// </remarks>
        /// <param name="parameters">Admin Credentials, Lifetime, User</param>
        /// <returns>User jwt, if credentials are vaild.</returns>
        [HttpPost("GetForUser")]
        public async Task<ActionResult<string>> GetAsyncForUser([FromBody] AuthenticationTokenGetForUserParameters parameters)
        {
            try
            {
                string adminUsername = parameters.AdminUsername;
                string adminPassword = parameters.AdminPassword;
                TimeSpan lifetime = parameters.Lifetime;
                string targetUserName = parameters.TargetUserName;
                string targetUserDn = parameters.TargetUserDn;

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);
                UiUser adminUser = new() { Name = adminUsername, Password = adminPassword };
                // Check if admin valids are valid
                try
                {
                    UiUser authenticatedAdminUser = await authManager.AuthenticateAndBuildUserAsync(adminUser, validatePassword: true)
                        ?? throw new AuthenticationException("Provided admin credentials are invalid.");
                    if (!authenticatedAdminUser.Roles.Contains(Roles.Admin))
                    {
                        throw new AuthenticationException("Provided credentials do not belong to a user with role admin.");
                    }
                    adminUser = authenticatedAdminUser;
                }
                catch (Exception e)
                {
                    throw new AuthenticationException("Error while validating admin credentials: " + e.Message);
                }
                // Check if username is valid and generate jwt
                try
                {
                    UiUser targetUser = new() { Name = targetUserName, Dn = targetUserDn };
                    UiUser authenticatedTargetUser = await authManager.AuthenticateAndBuildUserAsync(targetUser, validatePassword: false)
                        ?? throw new AuthenticationException("Provided target user credentials are invalid.");
                    TimeSpan delegatedLifetime = tokenLifetimeProvider.CapDelegatedUserTokenLifetime(lifetime);
                    string jwt = jwtWriter.CreateJWT(authenticatedTargetUser, delegatedLifetime);
                    WriteJwtAudit("IssueDelegatedAccessToken", jwt, adminUser,
                        $"Issued delegated access token for target user \"{authenticatedTargetUser.Name}\".");
                    return Ok(jwt);
                }
                catch (Exception e)
                {
                    throw new AuthenticationException("Error while validating user credentials (user name): " + e.Message);
                }
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

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);

                // Validate refresh token
                RefreshTokenInfo? tokenInfo = await authManager.ValidateRefreshToken(request.RefreshToken);

                if (tokenInfo == null)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                UiUser[] users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = tokenInfo.UserId });
                UiUser? storedUser = users.FirstOrDefault();

                if (storedUser == null)
                {
                    return Unauthorized("User not found");
                }

                UiUser? user = await authManager.AuthenticateAndBuildUserAsync(storedUser, validatePassword: false, updateLoginState: false);

                if (user == null)
                {
                    return Unauthorized("User could not be reconstructed for refresh");
                }

                // Consume the old refresh token exactly once before minting a new pair.
                int revokedTokens = await authManager.RevokeRefreshToken(request.RefreshToken);

                if (revokedTokens != 1)
                {
                    Log.WriteWarning("Token Refresh", $"Refresh token for user {user.Name} was already consumed or revoked.");
                    return Unauthorized("Invalid or expired refresh token");
                }

                // Create new token pair
                TokenPair newTokens = await authManager.CreateTokenPair(user);

                Log.WriteInfo("Token Refresh", $"Successfully refreshed tokens for user {user.Name}");
                WriteTokenPairAudit("RefreshTokenPair", newTokens, user, "Refreshed token pair after refresh-token rotation.");
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

                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);

                RefreshTokenInfo? tokenInfo = await authManager.ValidateRefreshToken(request.RefreshToken);

                if (tokenInfo == null)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                UiUser? auditUser = null;
                UiUser[] revokeUsers = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = tokenInfo.UserId });
                auditUser = revokeUsers.FirstOrDefault();

                int revokedTokens = await authManager.RevokeRefreshToken(request.RefreshToken);

                if (revokedTokens != 1)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                Log.WriteInfo("Token Refresh", $"Successfully revoked refresh token");
                WriteAudit("RevokeRefreshToken",
                    $"Revoked refresh token for user_id={tokenInfo.UserId}.",
                    auditUser);

                return Ok();
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Failed to refresh token", ex);
                return BadRequest(ex.Message);
            }
        }

#if DEBUG
        /// <summary>
        ///  Tests the Auth from swagger. If this returns unauthorized then check JWT token in swagger and try again.
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpGet("TestAuth")]
        public async Task<ActionResult> TestAuth()
        {
            return Ok();
        }
#endif
        /// <summary>
        /// Reads a JWT without validating it so audit logging can extract metadata such as jti and expiry.
        /// </summary>
        /// <param name="jwt">JWT to inspect.</param>
        /// <returns>Parsed JWT.</returns>
        private static JwtSecurityToken ReadJwt(string jwt)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        }

        /// <summary>
        /// Builds the audit text for an issued access and optional refresh token pair.
        /// </summary>
        /// <param name="tokenPair">Issued token pair.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        /// <returns>Audit message text containing jti and expiry information.</returns>
        private static string BuildTokenPairAuditText(TokenPair tokenPair, string actionText)
        {
            JwtSecurityToken accessToken = ReadJwt(tokenPair.AccessToken);
            string auditText = $"{actionText} access_jti={accessToken.Id}, access_expires={accessToken.ValidTo:O}";
            if (tokenPair.RefreshTokenExpires != DateTime.MinValue)
            {
                auditText += $", refresh_expires={tokenPair.RefreshTokenExpires:O}";
            }

            return auditText;
        }

        /// <summary>
        /// Writes an audit entry for an issued access and optional refresh token pair.
        /// </summary>
        /// <param name="title">Audit title.</param>
        /// <param name="tokenPair">Issued token pair.</param>
        /// <param name="actingUser">User that triggered the issuance, if available.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        private static void WriteTokenPairAudit(string title, TokenPair tokenPair, UiUser? actingUser, string actionText)
        {
            WriteAudit(title, BuildTokenPairAuditText(tokenPair, actionText), actingUser);
        }

        /// <summary>
        /// Builds the audit text for an issued access token.
        /// </summary>
        /// <param name="jwt">Issued JWT.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        /// <returns>Audit message text containing jti and expiry information.</returns>
        private static string BuildJwtAuditText(string jwt, string actionText)
        {
            JwtSecurityToken accessToken = ReadJwt(jwt);
            return $"{actionText} access_jti={accessToken.Id}, access_expires={accessToken.ValidTo:O}";
        }

        /// <summary>
        /// Writes an audit entry for an issued access token.
        /// </summary>
        /// <param name="title">Audit title.</param>
        /// <param name="jwt">Issued JWT.</param>
        /// <param name="actingUser">User that triggered the issuance, if available.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        private static void WriteJwtAudit(string title, string jwt, UiUser? actingUser, string actionText)
        {
            WriteAudit(title, BuildJwtAuditText(jwt, actionText), actingUser);
        }

        /// <summary>
        /// Writes an audit entry either with actor identity data or anonymously when no actor is available.
        /// </summary>
        /// <param name="title">Audit title.</param>
        /// <param name="text">Audit text.</param>
        /// <param name="actingUser">User that triggered the action, if available.</param>
        private static void WriteAudit(string title, string text, UiUser? actingUser)
        {
            if (actingUser != null)
            {
                Log.WriteAudit(title, text, actingUser.Name, actingUser.Dn);
                return;
            }

            Log.WriteAudit(title, text);
        }
    }

    class AuthManager
    {
        private readonly JwtWriter jwtWriter;
        private readonly List<Ldap> ldaps;
        private readonly ApiConnection apiConnection;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private readonly string UserAuthentication = "User Authentication";

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

        public async Task<List<string>> GetGroups(LdapEntry ldapUser, Ldap ldap)
        {
            HashSet<string> userGroups = new(StringComparer.OrdinalIgnoreCase);
            userGroups.UnionWith(ldap.GetGroups(ldapUser));
            if (userGroups.Count == 0)
            {
                string? groupPath = !string.IsNullOrWhiteSpace(ldap.GroupSearchPath) ? ldap.GroupSearchPath : ldap.GroupWritePath;
                List<string> groupNames = await ldap.GetGroups([ldapUser.Dn]);
                userGroups.UnionWith(Ldap.BuildGroupDns(groupNames, groupPath));
            }
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
                            string? groupPath = !string.IsNullOrWhiteSpace(currentLdap.GroupSearchPath) ? currentLdap.GroupSearchPath : currentLdap.GroupWritePath;
                            userGroups.UnionWith(Ldap.BuildGroupDns(currentGroups, groupPath));
                        }
                    }));
                }
                await Task.WhenAll(ldapRoleRequests);
            }
            return userGroups.ToList();
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
        private static string GenerateTokenHash(string token)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Creates an access-token and refresh-token pair for the given user.
        /// </summary>
        /// <param name="user">The authenticated user for whom the token pair is created. If null, an anonymous access token without a refresh token is created.</param>
        /// <param name="accessTokenLifetime">Optional access-token lifetime override.</param>
        /// <returns>A token pair containing the signed access token and, for authenticated users, a persisted refresh token with its expiration metadata.</returns>
        public async Task<TokenPair> CreateTokenPair(UiUser? user = null, TimeSpan? accessTokenLifetime = null)
        {
            TimeSpan accessLifetime = user == null
                ? tokenLifetimeProvider.GetAnonymousTokenLifetime()
                : accessTokenLifetime ?? await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);

            string accessToken = jwtWriter.CreateJWT(user, accessLifetime);

            JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            string refreshToken = "";
            DateTime refreshExpiry = DateTime.MinValue;

            if (user is not null)
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
