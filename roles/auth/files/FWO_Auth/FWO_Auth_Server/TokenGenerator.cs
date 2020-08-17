using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
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
            this.daysValid = daysValid;
        }

        public async Task<string> CreateJWTAsync(User user, UserData userData, Role[] roles)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = await CreateClaimsIdentities(user, userData, roles);

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (               
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(daysValid),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.HmacSha512Signature)               
             );

            return tokenHandler.WriteToken(token);
        }

        private Task<ClaimsIdentity> CreateClaimsIdentities(User user, UserData userData, Role[] roles)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.UserData, JsonSerializer.Serialize(userData)));          

            foreach (Role role in roles)
            { 
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
                claimsIdentity.AddClaim(new Claim("x-hasura-role", role.Name)); // Hasura Role
            }

            return Task.FromResult(claimsIdentity);
        }
    }
}
