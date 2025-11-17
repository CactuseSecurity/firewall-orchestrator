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
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
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

        public async Task<RestResponse<TokenPair>> Authenticate(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient,
			GlobalConfig globalConfig, UserConfig userConfig, CircuitHandlerService circuitHandler)
		{
			// There is no jwt in session storage. Get one from auth module.
			AuthenticationTokenGetParameters authenticationParameters = new() { Username = username, Password = password };
			RestResponse<TokenPair> apiAuthResponse = await middlewareClient.AuthenticateUser(authenticationParameters);

			if (apiAuthResponse.StatusCode == HttpStatusCode.OK)
			{
                string tokenPairJson = apiAuthResponse.Content ?? throw new ArgumentException("no response content");

                TokenPair tokenPair = System.Text.Json.JsonSerializer.Deserialize<TokenPair>(tokenPairJson) ?? throw new ArgumentException("failed to deserialize token pair");

                await tokenService.SetTokenPair(tokenPair);

                string jwtString = tokenPair.AccessToken ?? throw new ArgumentException("no access token in response");

                await Authenticate(jwtString, apiConnection, middlewareClient, globalConfig, userConfig, circuitHandler);

				Log.WriteAudit("AuthenticateUser", $"user {username} successfully authenticated");
			}

			return apiAuthResponse;
		}

		public async Task Authenticate(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient,
			GlobalConfig globalConfig, UserConfig userConfig, CircuitHandlerService circuitHandler)
		{
			// Try to auth with jwt (validates it and creates user context on UI side).
			JwtReader jwtReader = new(jwtString);

			if (await jwtReader.Validate())
			{
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

				// Tell api connection to use jwt as authentication
				apiConnection.SetAuthHeader(jwtString);

				// Tell middleware connection to use jwt as authentication
				middlewareClient.SetAuthenticationToken(jwtString);

				// Set user claims based on the jwt claims
				ClaimsIdentity identity = new ClaimsIdentity
				(
					claims: jwtReader.GetClaims(),
					authenticationType: "ldap",
					nameType: JwtRegisteredClaimNames.UniqueName,
					roleType: "role"
				);

				// Set user information
				user = new ClaimsPrincipal(identity);
				string userDn = user.FindFirstValue("x-hasura-uuid") ?? "";
				await userConfig.SetUserInformation(userDn, apiConnection);
				userConfig.User.Jwt = jwtString;
				userConfig.User.Tenant = await GetTenantFromJwt(userConfig.User.Jwt, apiConnection);
				userConfig.User.Roles = await GetAllowedRoles(userConfig.User.Jwt);
				userConfig.User.Ownerships = await GetAssignedOwners(userConfig.User.Jwt);
				circuitHandler.User = userConfig.User;

				if (!userConfig.User.PasswordMustBeChanged)
				{
					NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
				}
			}
			else
			{
				Deauthenticate();
			}
		}

		public void Deauthenticate()
		{
			user = new ClaimsPrincipal(new ClaimsIdentity());
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
		}

		public void ConfirmPasswordChanged()
		{
			NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user ?? throw new AuthenticationException("Password cannot be changed because user was not authenticated"))));
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
			List<int> ownerIds = [];
			List<string> ownerClaims = await GetClaimList(jwtString, "x-hasura-editable-owners");
			if (ownerClaims.Count > 0)
			{
				string[] separatingStrings = [",", "{", "}"];
				string[] owners = ownerClaims[0].Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
				ownerIds = Array.ConvertAll(owners, x => int.Parse(x)).ToList();
			}
			return ownerIds;
		}

		private static async Task<List<string>> GetClaimList(string jwtString, string claimType)
		{
			List<string> claimList = [];
			JwtReader jwtReader = new(jwtString);
			if (await jwtReader.Validate())
			{
				ClaimsIdentity identity = new
				(
					claims: jwtReader.GetClaims(),
					authenticationType: "ldap",
					nameType: JwtRegisteredClaimNames.UniqueName,
					roleType: "role"
				);
				foreach (Claim claim in identity.Claims)
				{
					if (claim.Type == claimType)
					{
						claimList.Add(claim.Value);
					}
				}
			}
			return claimList;
		}
    }
}

