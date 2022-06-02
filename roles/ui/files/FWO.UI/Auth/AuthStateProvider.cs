using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Ui.Services;
using FWO.Middleware.Client;
using FWO.Middleware.RequestParameters;
using RestSharp;
using System.Net;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private ClaimsPrincipal? authenticatedUser;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }

        public async Task AuthenticateUser(string jwtString, UserConfig userConfig, ApiConnection apiConnection, CircuitHandlerService circuitHandler)
        {
            JwtReader jwt = new JwtReader(jwtString);

            if (jwt.Validate())
            {
                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwt.GetClaims(),
                    authenticationType: "ldap",
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );

                authenticatedUser = new ClaimsPrincipal(identity);

                await userConfig.SetUserInformation(authenticatedUser.FindFirstValue("x-hasura-uuid"), apiConnection);
                circuitHandler.User = userConfig.User;
                userConfig.User.Jwt = jwtString;

                if(!userConfig.User.PasswordMustBeChanged)
                {
                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authenticatedUser)));
                }
            }

            else
            {
                Deauthenticate();
            }           
        }

        public async Task<RestResponse<string>> Login(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient,
            UserConfig userConfig, ProtectedSessionStorage sessionStorage, CircuitHandlerService circuitHandler)
        {
            // There is no jwt in session storage. Get one from auth module.
            AuthenticationTokenGetParameters authenticationParameters = new AuthenticationTokenGetParameters { Username = username, Password = password };
            RestResponse<string> apiAuthResponse = await middlewareClient.AuthenticateUser(authenticationParameters);

            if (apiAuthResponse.StatusCode == HttpStatusCode.OK)
            {
                Log.WriteAudit("AuthenticateUser", $"user {username} successfully authenticated");

                string jwt = apiAuthResponse.Data ?? throw new Exception("no response data");

                // Save it in session storage.
                await sessionStorage.SetAsync("jwt", jwt);

                // Add all user relevant information to the current session. Also used when reloading page.
                await CreateUserContext(jwt, apiConnection, middlewareClient, userConfig, circuitHandler);

                // Add jwt expiry timer
                JwtReader reader = new JwtReader(jwt);
                reader.Validate();
                JwtEventService.AddJwtTimer(userConfig.User.Dn, (int)reader.TimeUntilExpiry().TotalMilliseconds - 1000 * 60 * 5);
            }
            return apiAuthResponse;
        }

        public void Deauthenticate()
        {           
            ClaimsIdentity identity = new ClaimsIdentity();
            ClaimsPrincipal emptyUser = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(emptyUser)));
        }

        public async Task CreateUserContext(string jwt, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            // Tell api connection to use jwt as authentication
            apiConnection.SetAuthHeader(jwt);

            // Tell middleware connection to use jwt as authentication
            middlewareClient.SetAuthenticationToken(jwt);

            // Try to auth with jwt (validates it and creates user context on UI side).
            await AuthenticateUser(jwt, userConfig, apiConnection, circuitHandler);
        }

        public void ConfirmPasswordChanged()
        {           
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authenticatedUser ?? throw new Exception("Password cannot be changed because user was not authenticated"))));
        }
    }
}

