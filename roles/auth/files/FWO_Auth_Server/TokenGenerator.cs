using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;

namespace FWO_Auth_Server
{
    class TokenGenerator
    {
        // private readonly SymmetricSecurityKey privateJwtKey;
        // private readonly AsymmetricSignatureProvider publicJwtKey;
        private readonly RsaSecurityKey rsaSecurityKey;
        private readonly int hoursValid;
        private readonly string signingAlgorithm = SecurityAlgorithms.RsaSha256;
        private const string issuer = "FWO Auth Module";
        private const string audience = "FWO";

        public TokenGenerator(RsaSecurityKey rsaSecurityKey, int hoursValid)
        {
            this.hoursValid = hoursValid;
            this.rsaSecurityKey = rsaSecurityKey;
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
                expires: DateTime.UtcNow.AddHours(hoursValid),
                signingCredentials: new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256)
             );

            string GeneratedToken = tokenHandler.WriteToken(token);
            Console.WriteLine($"Generated JWT {GeneratedToken} for User {user.Name}");
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
            List<string> localRolesList = new List<string>();

            foreach (Role role in roles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name)); // Frontend Roles
                localRolesList.Add(role.Name); // Hasura Roles
            }

            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(localRolesList.ToArray()), JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array

            // deciding on default-role
            String defaultRole = "";
            if (roles != null && roles.Length > 0) 
            {
                if (localRolesList.Contains("reporter-viewall"))
                    defaultRole = "reporter-viewall";
                else {
                    if (localRolesList.Contains("reporter"))
                        defaultRole = "reporter";
                    else
                        defaultRole = roles[0].Name; // pick first role at random (todo: might need to be changed)
                }
            }
            else
            {
                Console.WriteLine($"ERROR: User {user.Name} does not have any assigned roles");    
            }

            claimsIdentity.AddClaim(new Claim("x-hasura-default-role", defaultRole));
            Console.WriteLine($"User {user.Name} was assigned default-role {defaultRole}");

            return claimsIdentity;
        }
    }
}
