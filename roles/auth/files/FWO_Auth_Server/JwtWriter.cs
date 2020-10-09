using FWO.Auth.Server.Data;
using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Auth.Server
{
    class JwtWriter
    {
        private const string issuer = "FWO Auth Module";
        private const string audience = "FWO";

        private readonly RsaSecurityKey jwtPrivateKey;
        private readonly int hoursValid;

        public JwtWriter(RsaSecurityKey jwtPrivateKey, int hoursValid)
        {
            this.hoursValid = hoursValid;
            this.jwtPrivateKey = jwtPrivateKey;
        }

        public string CreateJWT(User user)
        {
            Log.WriteDebug("Jwt generation", $"Generating JWT for user {user.Name} ...");

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = GetClaims(user);

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-5), // TODO: JUST FOR YANNIK
                issuedAt: DateTime.UtcNow.AddMinutes(-5),
                expires: DateTime.UtcNow.AddHours(hoursValid),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );

            string GeneratedToken = tokenHandler.WriteToken(token);

            Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for User {user.Name}");
            return GeneratedToken;
        }

        private ClaimsIdentity GetClaims(User user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));

            // TODO: Remove later
            // Fake managment claims REMOVE LATER 

            claimsIdentity.AddClaim(new Claim("x-hasura-visible-managements", "{1,7,17}"));
            claimsIdentity.AddClaim(new Claim("x-hasura-visible-devices", "{1,4}"));

            // adding roles
            string[] roles = user.Roles;

            // we need to create an extra list beacause hasura only accepts an array of roles even if there is only one
            List<string> hasuraRolesList = new List<string>();

            foreach (string role in roles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role)); // Frontend Roles
                hasuraRolesList.Add(role); // Hasura Roles
            }

            // add hasura roles claim as array
            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(hasuraRolesList.ToArray()), JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array

            // deciding on default-role
            string defaultRole = "";
            if (roles != null && roles.Length > 0) 
            {
                if (hasuraRolesList.Contains("reporter-viewall"))
                    defaultRole = "reporter-viewall";
                else {
                    if (hasuraRolesList.Contains("reporter"))
                        defaultRole = "reporter";
                    else
                        defaultRole = roles[0]; // pick first role at random (todo: might need to be changed)
                }
            }
            else
            {
                Log.WriteError("User roles", $"User {user.Name} does not have any assigned roles.");  
            }

            claimsIdentity.AddClaim(new Claim("x-hasura-default-role", defaultRole));
            Console.WriteLine($"User {user.Name} was assigned default-role {defaultRole}");

            return claimsIdentity;
        }
    }
}
