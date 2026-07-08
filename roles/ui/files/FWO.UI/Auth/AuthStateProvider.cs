using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Authentication;
using System.Security.Claims;

namespace FWO.Ui.Auth
{
    /// <summary>
    /// Manages the authenticated UI user state based on JWT access and refresh tokens.
    /// </summary>
    public class AuthStateProvider(TokenService tokenService, IEventMediator eventMediator, ExecutionModeStorage? executionModeStorage = null) : AuthenticationStateProvider
    {
        private enum JwtApplyStatus
        {
            Success,
            Expired,
            Invalid,
            UnauthorizedRole
        }

        private sealed class JwtApplyResult
        {
            public string? ErrorCode { get; init; }

            public JwtApplyStatus Status { get; init; }
        }

        private ClaimsPrincipal user = new(new ClaimsIdentity());

        /// <summary>
        /// Returns the current UI authentication state.
        /// </summary>
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return await Task.FromResult(new AuthenticationState(user));
        }

        /// <summary>
        /// Authenticates a user against the middleware and applies the returned token pair to the UI state.
        /// </summary>
        /// <param name="username">Login name supplied by the user.</param>
        /// <param name="password">Password supplied by the user.</param>
        /// <param name="apiConnection">API connection that should receive the JWT.</param>
        /// <param name="middlewareClient">Middleware client used for the login request.</param>
        /// <param name="userConfig">Current user configuration to rebuild from the JWT claims.</param>
        /// <returns>The middleware response containing the login result.</returns>
        public async Task<RestResponse<TokenPair>> Authenticate(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig)
        {
            // There is no jwt in session storage. Get one from auth module.
            AuthenticationTokenGetParameters authenticationParameters = new() { Username = username, Password = password };
            RestResponse<TokenPair> apiAuthResponse = await middlewareClient.AuthenticateUser(authenticationParameters);

            if (apiAuthResponse.StatusCode == HttpStatusCode.OK)
            {
                string tokenPairJson = apiAuthResponse.Content ?? throw new ArgumentException("no response content");

                TokenPair tokenPair = System.Text.Json.JsonSerializer.Deserialize<TokenPair>(tokenPairJson) ?? throw new ArgumentException("failed to deserialize token pair");

                if (string.IsNullOrWhiteSpace(tokenPair.AccessToken))
                {
                    throw new ArgumentException("no access token in response");
                }
                else if (string.IsNullOrWhiteSpace(tokenPair.RefreshToken))
                {
                    throw new ArgumentException("no refresh token in response");
                }

                await ApplyTokenPair(tokenPair, apiConnection, middlewareClient, userConfig);

                Log.WriteAudit("AuthenticateUser", $"User \"{username}\" with DN: \"{userConfig.User.Dn}\" successfully authenticated.");
            }

            return apiAuthResponse;
        }

        /// <summary>
        /// Restores the UI authentication state from the persisted token pair.
        /// </summary>
        /// <param name="apiConnection">API connection that should receive the restored JWT.</param>
        /// <param name="middlewareClient">Middleware client that should receive the restored JWT.</param>
        /// <param name="userConfig">Current user configuration to rebuild from the restored JWT.</param>
        /// <returns>True if a valid stored or refreshed JWT could be applied; otherwise false.</returns>
        public async Task<bool> RestoreAuthenticationState(ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig)
        {
            TokenPair? storedTokenPair = await tokenService.GetTokenPair();

            if (storedTokenPair == null)
            {
                return false;
            }

            if (!await tokenService.IsAccessTokenExpired())
            {
                JwtApplyResult applyStoredTokenResult = await ApplyJwtAsync(storedTokenPair.AccessToken, apiConnection, middlewareClient, userConfig);
                return await HandleRestoreResult(applyStoredTokenResult);
            }

            TokenPair? refreshedTokenPair = await tokenService.RefreshTokenPair();

            if (refreshedTokenPair == null ||
                string.IsNullOrWhiteSpace(refreshedTokenPair.AccessToken) ||
                string.IsNullOrWhiteSpace(refreshedTokenPair.RefreshToken))
            {
                await HandleExpiredSessionAsync();
                return false;
            }

            JwtApplyResult applyRefreshedTokenResult = await ApplyJwtAsync(refreshedTokenPair.AccessToken, apiConnection, middlewareClient, userConfig);
            return await HandleRestoreResult(applyRefreshedTokenResult);
        }

        /// <summary>
        /// Deauthenticates the current user and clears any persisted session state.
        /// </summary>
        public async Task Deauthenticate()
        {
            try
            {
                await tokenService.RevokeTokens();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Deauthenticate", $"Token cleanup failed during logout: {ex.Message}");
            }
            finally
            {
                if (executionModeStorage != null)
                {
                    await executionModeStorage.ClearExecutionMode();
                }

                user = new ClaimsPrincipal(new ClaimsIdentity());

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }
        }

        /// <summary>
        /// Re-emits the current authentication state after a successful password change flow.
        /// </summary>
        public void ConfirmPasswordChanged()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user ?? throw new AuthenticationException("Password cannot be changed because user was not authenticated"))));
        }

        /// <summary>
        /// Validates a JWT and applies its claims to the UI authentication state.
        /// </summary>
        /// <param name="jwtString">The JWT to validate and apply.</param>
        /// <param name="apiConnection">API connection that should receive the JWT.</param>
        /// <param name="middlewareClient">Middleware client that should receive the JWT.</param>
        /// <param name="userConfig">Current user configuration to rebuild from the JWT claims.</param>
        /// <returns>The outcome of the JWT validation and apply flow.</returns>
        private async Task<JwtApplyResult> ApplyJwtAsync(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig)
        {
            JwtReader jwtReader = new(jwtString);
            JwtValidationResult validationResult = await jwtReader.ValidateToken();

            if (validationResult.Status == JwtValidationStatus.Expired)
            {
                return new JwtApplyResult { Status = JwtApplyStatus.Expired };
            }

            if (validationResult.Status == JwtValidationStatus.Invalid)
            {
                return new JwtApplyResult { Status = JwtApplyStatus.Invalid };
            }

            // importer is not allowed to login
            if (jwtReader.ContainsRole(Roles.Importer))
            {
                return new JwtApplyResult
                {
                    Status = JwtApplyStatus.UnauthorizedRole,
                    ErrorCode = "login_importer_error"
                };
            }

            // anonymous has no authorization to login via UI
            if (jwtReader.ContainsRole(Roles.Anonymous))
            {
                return new JwtApplyResult
                {
                    Status = JwtApplyStatus.UnauthorizedRole,
                    ErrorCode = "not_authorized"
                };
            }

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

            await apiConnection.ReconnectSubscriptionsAsync(jwtString, cts.Token);
            middlewareClient.SetAuthenticationToken(jwtString);

            ClaimsIdentity identity = new
            (
                claims: jwtReader.GetClaims(),
                authenticationType: "ldap",
                nameType: JwtRegisteredClaimNames.UniqueName,
                roleType: "role"
            );

            user = new ClaimsPrincipal(identity);

            string userDn = user.FindFirstValue("x-hasura-uuid") ?? "";
            string defaultRole = user.FindFirstValue("x-hasura-default-role") ?? "";

            userConfig.User.Jwt = jwtString;
            await apiConnection.RunWithRole(defaultRole, async () =>
            {
                await userConfig.SetUserInformation(userDn, apiConnection);
                userConfig.User.Tenant = await GetTenantFromJwt(jwtString, apiConnection);
            });

            userConfig.User.Jwt = jwtString;
            userConfig.User.Roles = await GetAllowedRoles(userConfig.User.Jwt);
            userConfig.User.Ownerships = await GetAssignedOwners(userConfig.User.Jwt);
            userConfig.User.RecertOwnerships = await GetRecertifiableOwners(userConfig.User.Jwt);
            userConfig.User.WorkflowVisibilityGroupIds = await GetWorkflowVisibilityGroupIds(userConfig.User.Jwt);

            Log.WriteDebug("Auth Claims", $"Parsed allowed roles: [{string.Join(", ", userConfig.User.Roles)}]");
            Log.WriteDebug("Auth Claims", $"Parsed editable owners: [{string.Join(", ", userConfig.User.Ownerships)}]");
            Log.WriteDebug("Auth Claims", $"Parsed recertifiable owners: [{string.Join(", ", userConfig.User.RecertOwnerships)}]");
            Log.WriteDebug("Auth Claims", $"Parsed workflow visibility groups: [{string.Join(", ", userConfig.User.WorkflowVisibilityGroupIds)}]");

            await RestoreExecutionMode(apiConnection, userConfig);

            if (!userConfig.User.PasswordMustBeChanged)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }

            return new JwtApplyResult { Status = JwtApplyStatus.Success };
        }

        /// <summary>
        /// Handles the outcome of restoring a persisted or refreshed JWT.
        /// </summary>
        /// <param name="result">Validation outcome returned by <see cref="ApplyJwtAsync"/>.</param>
        /// <returns>True if the authentication state was restored successfully; otherwise false.</returns>
        private async Task<bool> HandleRestoreResult(JwtApplyResult result)
        {
            switch (result.Status)
            {
                case JwtApplyStatus.Success:
                    return true;
                case JwtApplyStatus.Expired:
                    await HandleExpiredSessionAsync();
                    return false;
                case JwtApplyStatus.Invalid:
                case JwtApplyStatus.UnauthorizedRole:
                    await Deauthenticate();
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Clears unusable tokens and notifies the UI when automatic session recovery is no longer possible.
        /// </summary>
        private async Task HandleExpiredSessionAsync()
        {
            PublishReloginRequiredForAuthenticatedUser();
            await tokenService.RevokeTokens();
        }

        /// <summary>
        /// Restores a valid execution mode for the authenticated user.
        /// </summary>
        /// <param name="apiConnection">API connection that should receive the execution mode.</param>
        /// <param name="userConfig">Current user configuration that stores the selected execution mode.</param>
        private async Task RestoreExecutionMode(ApiConnection apiConnection, UserConfig userConfig)
        {
            string storedExecutionMode = executionModeStorage != null
                ? await executionModeStorage.GetExecutionMode() ?? GlobalConst.kUserRolesSelection
                : GlobalConst.kUserRolesSelection;
            string executionMode = ExecutionModeHelper.NormalizeExecutionMode(userConfig.User.Roles, storedExecutionMode);

            apiConnection.SetExecutionMode(user, executionMode);
            userConfig.SetExecutionMode(executionMode);

            if (executionModeStorage != null)
            {
                await executionModeStorage.SetExecutionMode(executionMode);
            }
        }

        /// <summary>
        /// Applies a validated login response to the UI state and persists the token pair only after login is accepted.
        /// </summary>
        /// <param name="tokenPair">Token pair returned from the middleware login endpoint.</param>
        /// <param name="apiConnection">API connection that should receive the JWT.</param>
        /// <param name="middlewareClient">Middleware client that should receive the JWT.</param>
        /// <param name="userConfig">Current user configuration to rebuild from the JWT claims.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ApplyTokenPair(TokenPair tokenPair, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig)
        {
            JwtApplyResult result = await ApplyJwtAsync(tokenPair.AccessToken, apiConnection, middlewareClient, userConfig);

            switch (result.Status)
            {
                case JwtApplyStatus.Success:
                    await tokenService.SetTokenPair(tokenPair);
                    return;
                case JwtApplyStatus.UnauthorizedRole:
                    await Deauthenticate();
                    throw new AuthenticationException(result.ErrorCode ?? "not_authorized");
                case JwtApplyStatus.Expired:
                case JwtApplyStatus.Invalid:
                    await Deauthenticate();
                    throw new AuthenticationException("not_authorized");
            }

            throw new InvalidOperationException($"Unexpected JWT apply status: {result.Status}");
        }

        /// <summary>
        /// Announces that the current authenticated session now requires a re-login.
        /// </summary>
        private void PublishReloginRequiredForAuthenticatedUser()
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return;
            }

            string userDn = user.FindFirstValue("x-hasura-uuid") ?? "";

            if (string.IsNullOrWhiteSpace(userDn))
            {
                return;
            }

            PublishReloginRequired(userDn);
        }

        /// <summary>
        /// Announces that the current user's session can no longer be restored automatically and a re-login is required.
        /// </summary>
        /// <param name="userDn">Distinguished name of the affected user.</param>
        private void PublishReloginRequired(string userDn) => eventMediator.Publish(nameof(ReloginRequiredEvent), new ReloginRequiredEvent(new(userDn)));

        // public async Task<int> GetTenantId(string jwtString)
        // {
        // 	JwtReader jwtReader = new(jwtString);
        // 	int tenantId = 0;

        // 	if (await jwtReader.Validate())
        // 	{
        // 		ClaimsIdentity identity = new
        // 		(
        // 			claims: jwtReader.GetClaims(),
        // 			authenticationType: "ldap",
        // 			nameType: JwtRegisteredClaimNames.UniqueName,
        // 			roleType: "role"
        // 		);

        // 		// Set user information
        // 		user = new ClaimsPrincipal(identity);

        // 		if (!int.TryParse(user.FindFirstValue("x-hasura-tenant-id"), out tenantId))
        // 		{
        // 			// TODO: log warning
        // 		}
        // 	}
        // 	return tenantId;
        // }

        private async Task<Tenant> GetTenantFromJwt(string jwtString, ApiConnection apiConnection)
        {
            JwtReader jwtReader = new(jwtString);
            Tenant tenant = new();

            if ((await jwtReader.ValidateToken()).IsSuccess)
            {
                ClaimsIdentity identity = new
                (
                    claims: jwtReader.GetClaims(),
                    authenticationType: "ldap",
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );

                // Set user information
                user = new ClaimsPrincipal(identity);

                if (int.TryParse(user.FindFirstValue("x-hasura-tenant-id"), out int tenantId))
                {
                    tenant = await GetSingleTenant(apiConnection, tenantId) ?? new();
                }
                // else
                // {
                //     // TODO: log warning
                // }
            }
            return tenant;
        }

        public static async Task<Tenant?> GetSingleTenant(ApiConnection conn, int tenantId)
        {
            Tenant[] tenants = await conn.SendQueryAsync<Tenant[]>(AuthQueries.getTenants, new { tenant_id = tenantId });
            if (tenants.Length > 0)
            {
                return tenants[0];
            }
            else
            {
                return null;
            }
        }

        private static async Task<List<string>> GetAllowedRoles(string jwtString)
        {
            return await GetClaimList(jwtString, "x-hasura-allowed-roles");
        }

        private static async Task<List<int>> GetAssignedOwners(string jwtString)
        {
            return await GetIntClaimList(jwtString, "x-hasura-editable-owners");
        }

        private static async Task<List<int>> GetRecertifiableOwners(string jwtString)
        {
            return await GetIntClaimList(jwtString, "x-hasura-recertifiable-owners");
        }

        private static async Task<List<int>> GetWorkflowVisibilityGroupIds(string jwtString)
        {
            return await GetIntClaimList(jwtString, "x-hasura-workflow-visibility-groups");
        }

        private static async Task<List<string>> GetClaimList(string jwtString, string claimType)
        {
            JwtReader jwtReader = new(jwtString);
            if (!(await jwtReader.ValidateToken()).IsSuccess)
            {
                return [];
            }

            ClaimsIdentity identity = new
            (
                claims: jwtReader.GetClaims(),
                authenticationType: "ldap",
                nameType: JwtRegisteredClaimNames.UniqueName,
                roleType: "role"
            );
            return JwtClaimParser.ExtractStringClaimValues(identity.Claims, claimType);
        }

        private static async Task<List<int>> GetIntClaimList(string jwtString, string claimType)
        {
            JwtReader jwtReader = new(jwtString);
            if (!(await jwtReader.ValidateToken()).IsSuccess)
            {
                return [];
            }

            ClaimsIdentity identity = new
            (
                claims: jwtReader.GetClaims(),
                authenticationType: "ldap",
                nameType: JwtRegisteredClaimNames.UniqueName,
                roleType: "role"
            );
            return JwtClaimParser.ExtractIntClaimValues(identity.Claims, claimType);
        }
    }
}
