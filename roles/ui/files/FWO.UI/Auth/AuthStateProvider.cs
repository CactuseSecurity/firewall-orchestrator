using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Authentication;
using System.Security.Claims;

namespace FWO.Ui.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = new(new ClaimsIdentity());

        private readonly TokenService tokenService;

        public AuthStateProvider(TokenService tokenService)
        {
            this.tokenService = tokenService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return await Task.FromResult(new AuthenticationState(user));
        }

        public async Task<RestResponse<TokenPair>> Authenticate(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient, GlobalConfig globalConfig, UserConfig userConfig, CircuitHandlerService circuitHandler)
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

                await tokenService.SetTokenPair(tokenPair);

                await Authenticate(tokenPair.AccessToken, apiConnection, middlewareClient, globalConfig, userConfig, circuitHandler);

                Log.WriteAudit("AuthenticateUser", $"user {username} successfully authenticated");
            }

            return apiAuthResponse;
        }

        public async Task Authenticate(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient, GlobalConfig globalConfig, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            if (await ApplyJwtAsync(jwtString, apiConnection, middlewareClient, userConfig, circuitHandler))
            {
                return;
            }

            await Deauthenticate();
        }

        /// <summary>
        /// Refreshes the token pair and applies the resulting JWT to the UI authentication state.
        /// </summary>
        /// <param name="apiConnection">API connection that should receive the refreshed JWT.</param>
        /// <param name="middlewareClient">Middleware client that should receive the refreshed JWT.</param>
        /// <param name="userConfig">Current user configuration to rebuild from the refreshed JWT.</param>
        /// <param name="circuitHandler">Circuit-scoped user context that should be updated after refresh.</param>
        /// <returns>True if a valid JWT could be refreshed and applied; otherwise false.</returns>
        public async Task<bool> RefreshAuthenticationState(ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            TokenPair? refreshedTokenPair = await tokenService.RefreshTokenPair();

            if (refreshedTokenPair == null || string.IsNullOrWhiteSpace(refreshedTokenPair.AccessToken))
            {
                return false;
            }

            return await ApplyJwtAsync(refreshedTokenPair.AccessToken, apiConnection, middlewareClient, userConfig, circuitHandler);
        }

        /// <summary>
        /// Deauthenticate the current user and clear session storage.
        /// </summary>
        /// <returns></returns>
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
                user = new ClaimsPrincipal(new ClaimsIdentity());

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }
        }

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
        /// <param name="circuitHandler">Circuit-scoped user context to update after the JWT is applied.</param>
        /// <returns>True if the JWT was valid and applied successfully; otherwise false.</returns>
        private async Task<bool> ApplyJwtAsync(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            JwtReader jwtReader = new(jwtString);

            if (!await jwtReader.Validate())
            {
                return false;
            }

            // importer is not allowed to login
            if (jwtReader.ContainsRole(Roles.Importer))
            {
                throw new AuthenticationException("login_importer_error");
            }

            // anonymous has no authorization to login via UI
            if (jwtReader.ContainsRole(Roles.Anonymous))
            {
                throw new AuthenticationException("not_authorized");
            }

            apiConnection.SetAuthHeader(jwtString);
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

            await userConfig.SetUserInformation(userDn, apiConnection);

            userConfig.User.Jwt = jwtString;
            userConfig.User.Tenant = await GetTenantFromJwt(userConfig.User.Jwt, apiConnection);
            userConfig.User.Roles = await GetAllowedRoles(userConfig.User.Jwt);
            userConfig.User.Ownerships = await GetAssignedOwners(userConfig.User.Jwt);
            userConfig.User.RecertOwnerships = await GetRecertifiableOwners(userConfig.User.Jwt);

            Log.WriteDebug("Auth Claims", $"Parsed allowed roles: [{string.Join(", ", userConfig.User.Roles)}]");
            Log.WriteDebug("Auth Claims", $"Parsed editable owners: [{string.Join(", ", userConfig.User.Ownerships)}]");
            Log.WriteDebug("Auth Claims", $"Parsed recertifiable owners: [{string.Join(", ", userConfig.User.RecertOwnerships)}]");

            circuitHandler.User = userConfig.User;

            if (!userConfig.User.PasswordMustBeChanged)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }

            return true;
        }

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

            if (await jwtReader.Validate())
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

        private static async Task<List<string>> GetClaimList(string jwtString, string claimType)
        {
            JwtReader jwtReader = new(jwtString);
            if (!await jwtReader.Validate())
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
            if (!await jwtReader.Validate())
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
