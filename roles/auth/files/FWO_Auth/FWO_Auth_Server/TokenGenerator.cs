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
        public static async Task Main(string[] args)
        {
            User user = new User { Id = 1, Name = "Name", Password = "Password" };
            UserData userData = new UserData { SomeOtherData = 123 };
            string issuer = "FWO Auth Module";
            string audience = "FWO";
            int daysValid = 7;

            string privateKeyString = "J6k2eVCTXDp5b97u6gNH5GaaqHDxCmzz2wv3PRPFRsuW2UavK8LGPRauC4VSeaetKTMtVmVzAC8fh8Psvp8PFybEvpYnULHfRpM8TA2an7GFehrLLvawVJdSRqh2unCnWehhh2SJMMg5bktRRapA8EGSgQUV8TCafqdSEHNWnGXTjjsMEjUpaxcADDNZLSYPMyPSfp6qe5LMcd5S9bXH97KeeMGyZTS2U8gp3LGk2kH4J4F3fsytfpe9H9qKwgjb";
            SymmetricSecurityKey privateKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(privateKeyString));     

            string Jwt = await CreateJWTAsync(user, userData, issuer, audience, privateKey, daysValid);

            await Console.Out.WriteLineAsync(Jwt);
            await Console.In.ReadLineAsync();
        }

        public static async Task<string> CreateJWTAsync(User user, UserData userData, string issuer, string audience,
                                                        SymmetricSecurityKey privateKey, int daysValid)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = await CreateClaimsIdentities(user, userData);

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(daysValid),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.HmacSha256Signature)
             );

            return tokenHandler.WriteToken(token);
        }

        public static Task<ClaimsIdentity> CreateClaimsIdentities(User user, UserData userData)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            // Check if correct (LDAP)

            // Check if correct (LDAP)

            // Get Roles / User Data
            var roles = Enumerable.Empty<Role>();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.UserData, JsonSerializer.Serialize(userData)));
            // Get Roles / User Data

            foreach (var role in roles)
            { 
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
            }

            return Task.FromResult(claimsIdentity);
        }
    }
}
