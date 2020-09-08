using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        public string CreateJWT(User user, UserData userData, Role[] roles)
        {
            Console.WriteLine($"Generating JWT for user {user.Name} ...");

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
            //claimsIdentity.AddClaim(new Claim(ClaimTypes.UserData, JsonSerializer.Serialize(userData)));

            // TODO: Remove later
            // Fake managment claims REMOVE LATER 

            claimsIdentity.AddClaim(new Claim("x-hasura-visible-managements", "{1,7,17}"));
            claimsIdentity.AddClaim(new Claim("x-hasura-visible-devices", "{1,4}"));

            // adding roles:
            List<string> Roles = new List<string>();

            foreach (Role role in roles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name)); // Frontend Roles
                Roles.Add(role.Name); // Hasura Roles
            }

            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(Roles.ToArray()), JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array

            if (roles != null && roles.Length > 0)
                claimsIdentity.AddClaim(new Claim("x-hasura-default-role", roles[0].Name)); // Hasura default Role, pick first one at random (todo: needs to be changed)
            else 
                claimsIdentity.AddClaim(new Claim("x-hasura-default-role", "reporter"));

            return claimsIdentity;
        }
    }
}
