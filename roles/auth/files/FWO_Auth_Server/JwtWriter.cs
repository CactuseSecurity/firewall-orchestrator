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

        public string CreateJWT(User user)
        {
            Log.WriteDebug("Jwt generation", $"Generating JWT for user {user.Name} ...");
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject;
            if (user.Dn == "anonymous")
                subject = GetClaims(user);
            else
                subject = GetClaims(AddUserToDbAtFirstLogin(user));
                // adding uiuser.uiuser_id as x-hasura-user-id to JWT

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

        /// <summary>
        /// Jwt creator function used within authserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for auth-server role.</returns>
        public string CreateJWTAuthServer()
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = new ClaimsIdentity();
            subject.AddClaim(new Claim("unique_name", "auth-server"));
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

        /// <summary>
        /// if the user logs in for the first time, user details (excluding password) are written to DB bia API
        /// the database id is retrieved and added to the user 
        /// the user id is needed for allowing access to report_templates
        /// </summary>
        /// <returns> user including its db id </returns>
        private User AddUserToDbAtFirstLogin(User user)
        {
            if (user.Dn != "anonymous")
            {
                User userToBeReturned = new User();
                try
                {
                    APIConnection apiConn = new APIConnection(new ConfigConnection().ApiServerUri, CreateJWTAuthServer());
                    User[] existingUserFound = apiConn.SendQueryAsync<User[]>(BasicQueries.getUserByUuid, new { uuid = user.Dn }).Result;
                    if (existingUserFound.Length == 0)
                    {
                        Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to database.");
                        try               //    add new user to uiuser via API mutation
                        {
                            userToBeReturned = apiConn.SendQueryAsync<User[]>(BasicQueries.addUser, new { uuid = user.Dn, uiuser_username = user.Name }).Result[0];
                        }
                        catch (Exception addExeption)
                        {
                            Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", addExeption);
                        }
                    }
                    else
                    {
                        if (existingUserFound.Length == 1)
                            userToBeReturned = existingUserFound[0];
                        else
                            Log.WriteError("Duplicate User", $"User {user.Name} was found more than once!");
                    }
                }
                catch (Exception getException) //  if user.Dn does not exist in uiuser.uuid table, add it
                {
                    Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", getException);
                }
                // copy remaining fields from input user to returnUser:
                userToBeReturned.DefaultRole = user.DefaultRole;
                userToBeReturned.Roles = user.Roles;
                userToBeReturned.Tenant = user.Tenant;
                userToBeReturned.Password = user.Password;
                return userToBeReturned;
            }
            // for anonymous access, just return the unmodified user
            return user;
        }

        private ClaimsIdentity GetClaims(User user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim("x-hasura-user-id", user.DbId.ToString()));
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
