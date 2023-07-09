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

        public async Task<RestResponse<string>> Login(string username, string password, ApiConnection apiConnection, MiddlewareClient middlewareClient,
            UserConfig userConfig, ProtectedSessionStorage sessionStorage, CircuitHandlerService circuitHandler)
        {
            // There is no jwt in session storage. Get one from auth module.
            AuthenticationTokenGetParameters authenticationParameters = new AuthenticationTokenGetParameters { Username = username, Password = password };
            RestResponse<string> apiAuthResponse = await middlewareClient.AuthenticateUser(authenticationParameters);

            if (apiAuthResponse.StatusCode == HttpStatusCode.OK)
            {
                string jwt = apiAuthResponse.Data ?? throw new Exception("no response data");
				JwtReader reader = new JwtReader(jwt);
				reader.Validate();

                // importer is not allowed to login
				if (reader.ContainsRole("importer"))
                {
                    throw new AuthenticationException("login_importer_error");
                }

				Log.WriteAudit("AuthenticateUser", $"user {username} successfully authenticated");

				// Save it in session storage.
				await sessionStorage.SetAsync("jwt", jwt);

                // Add all user relevant information to the current session. Also used when reloading page.
                await CreateUserContext(jwt, apiConnection, middlewareClient, userConfig, circuitHandler);

                // Add jwt expiry timer
                JwtEventService.AddJwtTimers(userConfig.User.Dn, (int)reader.TimeUntilExpiry().TotalMilliseconds, 1000 * 60 * userConfig.SessionTimeoutNoticePeriod);
            }
            return apiAuthResponse;
        }

        public void Deauthenticate()
        {           
            user = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public async Task CreateUserContext(string jwtString, ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            // Try to auth with jwt (validates it and creates user context on UI side).
            JwtReader jwt = new JwtReader(jwtString);

            if (jwt.Validate())
            {
                // Tell api connection to use jwt as authentication
                apiConnection.SetAuthHeader(jwtString);

                // Tell middleware connection to use jwt as authentication
                middlewareClient.SetAuthenticationToken(jwtString);

                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwt.GetClaims(),
                    authenticationType: "ldap",
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );

                user = new ClaimsPrincipal(identity);

                await userConfig.SetUserInformation(user.FindFirstValue("x-hasura-uuid"), apiConnection);
                circuitHandler.User = userConfig.User;
                userConfig.User.Jwt = jwtString;

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

        public void ConfirmPasswordChanged()
        {           
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user ?? throw new Exception("Password cannot be changed because user was not authenticated"))));
        }
    }
}

