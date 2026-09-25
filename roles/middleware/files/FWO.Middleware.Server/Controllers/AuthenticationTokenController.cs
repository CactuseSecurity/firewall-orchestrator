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

        // Longer than a healthy refresh ever takes, short enough that a caller stuck behind
        // a hanging one gets an answer before its own HTTP timeout.
        private static readonly TimeSpan kRefreshLockTimeout = TimeSpan.FromSeconds(30);

        private const string kRefreshLogCategory = "Token Refresh";
        private const string kRevokeLogCategory = "Token Revoke";

        private enum RefreshTokenConsumptionState
        {
            NotAttempted,
            MayBeConsumed,
            Consumed
        }

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
            catch (Exception exception) when (ApiReachability.IndicatesUnreachableApi(exception))
            {
                // Same reasoning as for refresh: an unreachable API is not a malformed request,
                // and the transport error belongs in the log, not in the response body.
                Log.WriteError("Token Generation", "Could not reach the API while generating a token pair", exception);

                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "The API could not be reached while logging in. Please retry.");
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

                // Delegated tokens are not refreshable: withholding the refresh token prevents an admin-issued
                // short-lived delegated session from being rotated into a normal full-lifetime user session (#4654).
                TokenPair tokenPair = await authManager.CreateTokenPair(authenticatedTargetUser, issueRefreshToken: false);

                WriteTokenPairAudit("IssueDelegatedTokenPair", tokenPair, authenticatedAdminUser, $"Issued delegated token pair for target user \"{authenticatedTargetUser.Name}\".");

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
        /// Deprecated: This endpoint will be dropped in the next major release. Use /api/AuthenticationToken/GetTokenPair instead.
        ///
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

                TimeSpan accessLifetime = authenticatedUser == null ? tokenLifetimeProvider.GetAnonymousTokenLifetime() : await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);

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
        /// TargetUserDn OR TargetUserName (required) - Example: "uid=demo_user,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal" OR "demo_user" 
        /// </remarks>
        /// <param name="parameters">Admin credentials and target user identity.</param>
        /// <returns>User jwt, if credentials are valid.</returns>
        [HttpPost("GetForUser")]
        public async Task<ActionResult<string>> GetAsyncForUser([FromBody] AuthenticationTokenGetForUserParameters parameters)
        {
            try
            {
                string adminUsername = parameters.AdminUsername;
                string adminPassword = parameters.AdminPassword;
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

                    TimeSpan configuredLifetime = await tokenLifetimeProvider.GetUserAccessTokenLifetimeAsync(apiConnection);
                    string jwt = jwtWriter.CreateJWT(authenticatedTargetUser, configuredLifetime);

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
            // No HttpContext outside the ASP.NET pipeline, e.g. when a test calls the action.
            CancellationToken requestAborted = HttpContext?.RequestAborted ?? CancellationToken.None;
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest("Refresh token is required");
                }

                using IDisposable? refreshTokenLock = await RefreshTokenLockRegistry.TryAcquireAsync(
                    AuthManager.GenerateTokenHash(request.RefreshToken), kRefreshLockTimeout, requestAborted);
                if (refreshTokenLock == null)
                {
                    // Another request with this token has been running for too long; this one
                    // has not touched the token, so a retry is safe.
                    Log.WriteWarning(kRefreshLogCategory, $"Timed out after {kRefreshLockTimeout.TotalSeconds} s waiting for a concurrent refresh with the same token.");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        "Another refresh with this token is still in progress. Please retry.");
                }

                return await RefreshTokenCore(request.RefreshToken);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                // The caller disconnected while waiting; nobody is left to read an answer.
                return StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                Log.WriteError(kRefreshLogCategory, "Failed to refresh token", ex);
                return BadRequest("The refresh could not be completed.");
            }
        }

        /// <summary>
        /// Validates the refresh token, rebuilds its user and rotates the token pair.
        /// Must only run while the caller holds the lock of this token.
        /// </summary>
        /// <param name="refreshToken">Raw refresh token supplied by the caller.</param>
        /// <returns>
        /// The new token pair, 401 if the token is invalid or already consumed, or 503 if
        /// the API could not be reached before the token was consumed.
        /// </returns>
        private async Task<ActionResult<TokenPair>> RefreshTokenCore(string refreshToken)
        {
            // Track the consuming call separately from confirmed consumption because a
            // connection failure during the mutation cannot reveal whether it committed.
            RefreshTokenConsumptionState consumptionState = RefreshTokenConsumptionState.NotAttempted;

            // Captured as soon as the user is known so that the audit trail can name whose
            // session ended even when the failure happens later, outside the scope the user
            // is declared in.
            string auditIdentity = "";

            try
            {
                AuthManager authManager = new(jwtWriter, ldaps, apiConnection, tokenLifetimeProvider);

                // Validate refresh token
                RefreshTokenInfo? tokenInfo = await authManager.ValidateRefreshToken(refreshToken);

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

                auditIdentity = $"User \"{user.Name}\" with DN: \"{user.Dn}\"";

                // Consume the old refresh token exactly once before minting a new pair.
                consumptionState = RefreshTokenConsumptionState.MayBeConsumed;
                int revokedTokens = await authManager.RevokeRefreshToken(refreshToken);

                if (revokedTokens != 1)
                {
                    WriteAudit(nameof(RefreshToken), $"Refresh token for {auditIdentity} was already consumed or revoked.");

                    return Unauthorized("Invalid or expired refresh token");
                }

                consumptionState = RefreshTokenConsumptionState.Consumed;

                TokenPair newTokens = await authManager.CreateTokenPair(user);

                WriteAudit(nameof(RefreshToken), $"Successfully rotated auth tokens for {auditIdentity}.");

                return Ok(newTokens);
            }
            catch (Exception exception) when (ApiReachability.IndicatesUnreachableApi(exception))
            {
                // The API could not be reached, which is not the caller's fault: answering
                // 400 tells a client its request was malformed and must not be repeated,
                // while the truthful answer is that this attempt could not be completed.
                // Whether it may be repeated depends on how far it got, so say which.
                Log.WriteError(kRefreshLogCategory, "Could not reach the API while refreshing a token", exception);

                if (consumptionState == RefreshTokenConsumptionState.Consumed)
                {
                    // Audited like every other terminal outcome of this endpoint: a token
                    // was spent and a session ended, which is exactly what the trail is for.
                    WriteAudit(nameof(RefreshToken), $"Refresh token for {auditIdentity} was consumed, but no new token pair could be issued because the API could not be reached.");

                    // The token is spent, so it genuinely is invalid now, and 401 is both the
                    // truthful answer and the one a client can act on: a 503 here reads as
                    // retryable, so the client would hold on to a token that cannot work and
                    // reach the login one refresh cycle later than it needs to.
                    return Unauthorized("The refresh token was consumed, but the API could not be reached to issue a new token pair. Please log in again.");
                }

                if (consumptionState == RefreshTokenConsumptionState.MayBeConsumed)
                {
                    WriteAudit(nameof(RefreshToken), $"Refresh token for {auditIdentity} may have been consumed because the API became unreachable during the operation.");
                }

                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "The API could not be reached while refreshing the token. Please retry.");
            }
            catch (Exception ex)
            {
                Log.WriteError(kRefreshLogCategory, "Failed to refresh token", ex);
                return BadRequest("The refresh could not be completed.");
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
                auditUser = revokeUsers.FirstOrDefault() ?? new UiUser()
                {
                    Name = tokenInfo.UserId.ToString(),
                    Dn = ""
                };

                int revokedTokens;
                try
                {
                    revokedTokens = await authManager.RevokeRefreshToken(request.RefreshToken);
                }
                catch (Exception exception) when (ApiReachability.IndicatesUnreachableApi(exception))
                {
                    Log.WriteError(kRevokeLogCategory, "Could not reach the API while revoking a refresh token", exception);
                    WriteAudit(nameof(RevokeToken), $"Refresh token for User \"{auditUser.Name}\" with DN: \"{auditUser.Dn}\" may have been revoked because the API became unreachable during the operation.");

                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        "The API could not be reached while revoking the token. Please retry.");
                }

                if (revokedTokens != 1)
                {
                    WriteAudit(nameof(RevokeToken), $"Refresh token for User \"{auditUser.Name}\" with DN: \"{auditUser.Dn}\" was already consumed or revoked.");

                    return Unauthorized("Invalid or expired refresh token");
                }

                WriteAudit(nameof(RevokeToken), $"Revoked auth tokens for User \"{auditUser.Name}\" with DN: \"{auditUser.Dn}\".");

                return Ok();
            }
            catch (Exception exception) when (ApiReachability.IndicatesUnreachableApi(exception))
            {
                Log.WriteError(kRevokeLogCategory, "Could not reach the API while revoking a refresh token", exception);

                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "The API could not be reached while revoking the token. Please retry.");
            }
            catch (Exception ex)
            {
                Log.WriteError(kRevokeLogCategory, "Failed to revoke token", ex);
                return BadRequest("The revocation could not be completed.");
            }
        }

#if DEBUG
        /// <summary>
        ///  Tests the Auth from API docs. If this returns unauthorized then check JWT token in API docs and try again.
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
            string auditText = $"{actionText} access_jti={accessToken.Id}, access_expires={accessToken.ValidTo.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}";
            if (tokenPair.RefreshTokenExpires != DateTime.MinValue)
            {
                auditText += $", refresh_expires={tokenPair.RefreshTokenExpires.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}";
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
            return $"{actionText} access_jti={accessToken.Id}, access_expires={accessToken.ValidTo.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}";
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
        private static void WriteAudit(string title, string text, UiUser? actingUser = null)
        {
            if (actingUser != null)
            {
                Log.WriteAudit(title, text, actingUser.Name, actingUser.Dn);
            }
            else
            {
                Log.WriteAudit(title, text);
            }
        }
    }
}
