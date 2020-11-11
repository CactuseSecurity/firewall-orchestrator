using FWO.Auth.Server.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Config;
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
        /// <summary>
        /// Jwt creator function used within authserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for auth-server role.</returns>

        public string CreateJWTAuthServer()
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = new ClaimsIdentity();
            subject.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(new string[] { "auth-server" }), JsonClaimValueTypes.JsonArray));
            subject.AddClaim(new Claim("x-hasura-default-role", "auth-server"));

            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(1),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );
            string GeneratedToken = tokenHandler.WriteToken(token);
            Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for auth-server");
            return GeneratedToken;
        }

        public string CreateJWT(User user)
        {
            Log.WriteDebug("Jwt generation", $"Generating JWT for user {user.Name} ...");
            AddUserToDbAtFirstLogin(user);

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = GetClaims(user);

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddHours(hoursValid),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );

            string GeneratedToken = tokenHandler.WriteToken(token);

            Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for User {user.Name}");
            return GeneratedToken;
        }
        private User AddUserToDbAtFirstLogin(User user)
        {
            ConfigConnection config = new ConfigConnection();
            string apiUri = config.ApiServerUri;
            APIConnection apiConn = new APIConnection(apiUri, CreateJWTAuthServer());
            User newlyCreatedUser = null;
            if (user.Dn != "anonymous")
            {
                try
                {
                    var uuidVariable = new { uuid = user.Dn };
                    User[] existingUserFound = apiConn.SendQueryAsync<User[]>(BasicQueries.getUserByUuid, uuidVariable).Result;
                    if (existingUserFound.Length == 0)
                    {
                        Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to database.");
                        var newUserVariable = new { uuid = user.Dn, uiuser_username = user.Name };
                        try
                        {
                            User[] newlyCreateUsers = apiConn.SendQueryAsync<User[]>(BasicQueries.addUser, newUserVariable).Result;
                            newlyCreatedUser = newlyCreateUsers[0];
                        }
                        catch (Exception addExeption)
                        {
                            Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", addExeption);
                        }
                    }
                }
                catch (Exception getException) //  if user.Dn does not exist in uiuser.uuid table, add it
                {
                    Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", getException);
                }
                //    add new user to uiuser via API mutation
                // add uiuser.uiuser_id as x-hasura-user-id to JWT
            }
            return newlyCreatedUser;
        }
        private ClaimsIdentity GetClaims(User user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));

            if (user.Dn != null && user.Dn.Length > 0)
                claimsIdentity.AddClaim(new Claim("UUID", user.Dn));   // UUID used for access to reports via API
            if (user.Tenant != null)
            {
                // Hasura needs object {} instead of array [] notation      (TODO: Changable?)
                claimsIdentity.AddClaim(new Claim("x-hasura-tenant-id", user.Tenant.Id.ToString()));
                claimsIdentity.AddClaim(new Claim("x-hasura-visible-managements", $"{{ {string.Join(",", user.Tenant.VisibleManagements)} }}"));
                claimsIdentity.AddClaim(new Claim("x-hasura-visible-devices", $"{{ {string.Join(",", user.Tenant.VisibleDevices)} }}"));
            }

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
                else
                {
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
            // Log.WriteDebug("Default role assignment", $"User {user.Name} was assigned default-role {defaultRole}");
            return claimsIdentity;
        }
    }
}
