using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FWO_Auth_Client;
using Microsoft.AspNetCore.Components.Authorization;

namespace FWO.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            //var identity = new ClaimsIdentity(new[]
            //{
            //    new Claim(ClaimTypes.Name, "mrfibuli"),
            //}, "Fake authentication type");

            var identity = new ClaimsIdentity();

            var user = new ClaimsPrincipal(identity);

            return Task.FromResult(new AuthenticationState(user));            
        }

        public async void AuthenticateUser(string Username, string Password, AuthClient AuthClient)
        {           
            string auth_type = ""; // default = not authenticated = empty

            string JWT = await AuthClient.GetJWT(Username, Password);

            if (JWT != "")
            {
                auth_type = JWT;
            }       

            var identity = new ClaimsIdentity
            (
                new Claim[] { new Claim(ClaimTypes.Name, Username) }, 
                auth_type
            );
            // the above could have also be written as:
            // Claim[] claims = new Claim[1];
            // claims[0] = new Claim(ClaimTypes.Name, Username);
            // ClaimsIdentity identity2 = new ClaimsIdentity(claims, "Fake");
  
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
    }
}

