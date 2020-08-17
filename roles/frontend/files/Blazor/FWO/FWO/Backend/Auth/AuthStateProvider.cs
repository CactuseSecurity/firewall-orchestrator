using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FWO.Backend.Auth;
using FWO_Auth_Client;
using Microsoft.AspNetCore.Components.Authorization;

namespace FWO.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }

        public void AuthenticateUser(string Username, string Password, string JwtString)
        {
            Jwt jwt = new Jwt(JwtString);

            if (jwt.Valid())
            {
                ClaimsIdentity identity = new ClaimsIdentity
                (
                    claims: jwt.GetClaims(),
                    authenticationType: "usrname:" + Username
                );

                ClaimsPrincipal user = new ClaimsPrincipal(identity);

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }

            else
            {
                ClaimsIdentity identity = new ClaimsIdentity();
                ClaimsPrincipal user = new ClaimsPrincipal(identity);
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }
        }
    }
}

