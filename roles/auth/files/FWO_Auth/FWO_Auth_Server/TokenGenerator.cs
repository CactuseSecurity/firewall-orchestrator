using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO_Auth_Server
{
    class TokenGenerator
    {
        private readonly SymmetricSecurityKey privateKey;
        private readonly int daysValid;

        private const string issuer = "FWO Auth Module";
        private const string audience = "FWO";

        public TokenGenerator(string privateKey, int daysValid)
        {
            this.privateKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(privateKey));
#if DEBUG
            Console.WriteLine($"Read private jwt generation key from file: '{this.privateKey.Key.ToString()}' with size {this.privateKey.KeySize}");
#endif
            this.daysValid = daysValid;
        }

        public async Task<string> CreateJWTAsync(User user, UserData userData, Role[] roles)
        {
            Console.WriteLine($"Generating JWT for User {user}...");

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = CreateClaimsIdentities(user, userData, roles);

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(daysValid),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.HmacSha384)
             );

            string GeneratedToken = tokenHandler.WriteToken(token);

            Console.WriteLine($"Generated JWT {GeneratedToken} for User {user}");

            return GeneratedToken;
        }

        private ClaimsIdentity CreateClaimsIdentities(User user, UserData userData, Role[] roles)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.UserData, JsonSerializer.Serialize(userData)));

            // TODO: Remove later
            // Fake managment claims REMOVE LATER 
            claimsIdentity.AddClaim(new Claim("x-hasura-visible-managements", "{1,7,17}"));
            claimsIdentity.AddClaim(new Claim("x-hasura-visible-devices", "{1,4}"));
            // Fake managment claims REMOVE LATER

            // foreach (Role role in roles)
            // {
            //     // claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
            //     // TODO: Create API Connection Lib
            //     // TODO: Get Managment and Device Claims from API
            // }

            if (roles[0] != null)
                claimsIdentity.AddClaim(new Claim("x-hasura-default-role", roles[0].Name)); // Hasura default Role, pick first one at random (todo: needs to be changed)

            // adding allowed roles:

            String rolestring = "{";
            foreach (Role role in roles)
            {
                rolestring += role.Name + ",";
            }
            if (rolestring.Length>1)    // remove last comma
                rolestring = rolestring.Substring(0, rolestring.Length-1);
            rolestring += "}";

            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", rolestring)); // all roles the user is allowed to have
            return claimsIdentity;
        }
    }
}
