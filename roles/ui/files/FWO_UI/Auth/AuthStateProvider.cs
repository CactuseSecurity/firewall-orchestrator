using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Logging;
using FWO.ApiConfig;
using FWO.ApiClient;

namespace FWO.Ui.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }

        public async Task AuthenticateUser(string jwtString, UserConfig userConfig, APIConnection apiConnection)
        {
            JwtReader jwt = new JwtReader(jwtString);

            if (jwt.Validate())
            {
                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwt.GetClaims(),
                    authenticationType: "fake type", // TODO: change authentication type
                    nameType: JwtRegisteredClaimNames.UniqueName,
                    roleType: "role"
                );

                ClaimsPrincipal user = new ClaimsPrincipal(identity);

                await userConfig.SetUserInformation(user.FindFirstValue("x-hasura-uuid"), apiConnection);
                userConfig.User.Jwt = jwtString;

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }

            else
            {
                Deauthenticate();
            }           
        }

        public void Deauthenticate()
        {           
            ClaimsIdentity identity = new ClaimsIdentity();
            ClaimsPrincipal user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
    }
}

