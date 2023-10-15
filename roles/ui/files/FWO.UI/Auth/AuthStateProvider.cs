using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Ui.Services;
using FWO.Middleware.Client;
using FWO.Middleware.RequestParameters;
using RestSharp;
using System.Net;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Authentication;
using System.Security.Principal;


namespace FWO.Ui.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity());

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(user));
        }

        public async Task<RestResponse<string>> Authenticate(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient,
            GlobalConfig globalConfig, UserConfig userConfig, ProtectedSessionStorage sessionStorage, CircuitHandlerService circuitHandler)
        {
            // There is no jwt in session storage. Get one from auth module.
            AuthenticationTokenGetParameters authenticationParameters = new AuthenticationTokenGetParameters { Username = username, Password = password };
            RestResponse<string> apiAuthResponse = await middlewareClient.AuthenticateUser(authenticationParameters);

            if (apiAuthResponse.StatusCode == HttpStatusCode.OK)
            {
                string jwtString = apiAuthResponse.Data ?? throw new Exception("no response data");
                await Authenticate(jwtString, apiConnection, middlewareClient, globalConfig, userConfig, circuitHandler, sessionStorage);
                Log.WriteAudit("AuthenticateUser", $"user {username} successfully authenticated");
            }

            return apiAuthResponse;
        }

        public async Task Authenticate(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient,
            GlobalConfig globalConfig, UserConfig userConfig, CircuitHandlerService circuitHandler, ProtectedSessionStorage sessionStorage)
        {
            // Try to auth with jwt (validates it and creates user context on UI side).
            JwtReader jwtReader = new JwtReader(jwtString);

            if (jwtReader.Validate())
            {
                // importer is not allowed to login
                if (jwtReader.ContainsRole("importer"))
                {
                    throw new AuthenticationException("login_importer_error");
                }

                // Save jwt in session storage.
                await sessionStorage.SetAsync("jwt", jwtString);

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
                string userDn = user.FindFirstValue("x-hasura-uuid");
                await userConfig.SetUserInformation(userDn, apiConnection);
                userConfig.User.Jwt = jwtString;
                circuitHandler.User = userConfig.User;

                // Add jwt expiry timer
                JwtEventService.AddJwtTimers(userDn, (int)jwtReader.TimeUntilExpiry().TotalMilliseconds, 1000 * 60 * globalConfig.SessionTimeoutNoticePeriod);

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
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user ?? throw new Exception("Password cannot be changed because user was not authenticated"))));
        }


        public int getTenantId(string jwtString)
        {
            JwtReader jwtReader = new JwtReader(jwtString);
            int tenantId = 0;

            if (jwtReader.Validate())
            {
                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwtReader.GetClaims(),
                    authenticationType: "ldap",
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );

                // Set user information
                user = new ClaimsPrincipal(identity);

                if (!int.TryParse(user.FindFirstValue("x-hasura-tenant-id"), out tenantId))
                {
                    // TODO: log warning
                }
            }
            return tenantId;
        }
        public List<string> getAllowedRoles(string jwtString)
        {
            List<string> allowedRoles = new List<string>();
            JwtReader jwtReader = new JwtReader(jwtString);
            if (jwtReader.Validate())
            {
                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwtReader.GetClaims(),
                    authenticationType: "ldap",
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );
                foreach (Claim claim in identity.Claims)
                {
                    if (claim.Type == "x-hasura-allowed-roles")
                    {
                        allowedRoles.Add(claim.Value);
                    }
                }
            }
            return allowedRoles;
        }
    }
}

