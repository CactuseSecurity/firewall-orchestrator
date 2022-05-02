using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Ui.Services;
using FWO.Middleware.Client;

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

        public void Deauthenticate()
        {           
            ClaimsIdentity identity = new ClaimsIdentity();
            ClaimsPrincipal emptyUser = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(emptyUser)));
        }

        public void ConfirmPasswordChanged()
        {           
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authenticatedUser ?? throw new Exception("Password cannot be changed because user was not authenticated"))));
        }
    }
}

