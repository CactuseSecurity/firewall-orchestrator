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
using FWO.Api.Data;
using System.Threading.Tasks;

namespace FWO.Middleware.Server
{
    public class JwtWriter
    {
        private const string issuer = "FWO Middleware Module";
        private const string audience = "FWO";
        private readonly RsaSecurityKey jwtPrivateKey;
        private readonly int JwtMinutesValid;

        public JwtWriter(RsaSecurityKey jwtPrivateKey, int JwtMinutesValid)
        {
            this.JwtMinutesValid = JwtMinutesValid;
            this.jwtPrivateKey = jwtPrivateKey;
        }

        public async Task<string> CreateJWT(UiUser user = null)
        {
            if (user != null)
                Log.WriteDebug("Jwt generation", $"Generating JWT for user {user.Name} ...");
            else
                Log.WriteDebug("Jwt generation", "Generating empty JWT (startup)");

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            ClaimsIdentity subject;
            if (user != null)
                subject = GetClaims(await AddUserToDbAtFirstLogin(user));
            else
                subject = GetClaims(new UiUser() { Name = "", Password = "", Dn = "anonymous", Roles = new string[] { "anonymous" } });
            // adding uiuser.uiuser_id as x-hasura-user-id to JWT

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(JwtMinutesValid),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );

            string GeneratedToken = tokenHandler.WriteToken(token);
            if (user != null)
                Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for User {user.Name}");
            else
                Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken}");
            return GeneratedToken;
        }

        /// <summary>
        /// Jwt creator function used within middlewareserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for middleware-server role.</returns>
        public string CreateJWTMiddlewareServer()
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = new ClaimsIdentity();
            subject.AddClaim(new Claim("unique_name", "middleware-server"));
            subject.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(new string[] { "admin"/*"middleware-server"*/ }), JsonClaimValueTypes.JsonArray));
            subject.AddClaim(new Claim("x-hasura-default-role", "admin"/*"middleware-server"*/));

            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddYears(200),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );
            string GeneratedToken = tokenHandler.WriteToken(token);
            Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for middleware-server");
            return GeneratedToken;
        }

        /// <summary>
        /// if the user logs in for the first time, user details (excluding password) are written to DB bia API
        /// the database id is retrieved and added to the user 
        /// the user id is needed for allowing access to report_templates
        /// </summary>
        /// <returns> user including its db id </returns>
        private async Task<UiUser> AddUserToDbAtFirstLogin(UiUser user)
        {
            APIConnection apiConn = new APIConnection(new ConfigFile().ApiServerUri, CreateJWTMiddlewareServer());
            bool userSetInDb = false;
            try
            {
                UiUser[] existingUserFound = await apiConn.SendQueryAsync<UiUser[]>(AuthQueries.getUserByUuid, new { uuid = user.Dn });

                if (existingUserFound.Length == 1)
                {
                    user.DbId = existingUserFound[0].DbId;
                    await updateLastLogin(apiConn, user.DbId);
                    userSetInDb = true;
                }
                else
                {
                    Log.WriteError("User not found", $"Couldn't find {user.Name} exactly once!");
                }
            }
            catch(Exception exeption)
            {
                Log.WriteError("Get User Error", $"Error while trying to find {user.Name} in database.", exeption);
            }

            if(!userSetInDb)
            {
                Log.WriteInfo("New User", $"User {user.Name} first time log in - adding to database.");
                await addUser(apiConn, user);
            }
            // for anonymous access, just return the unmodified user
            return user;
        }

        private async Task addUser(APIConnection apiConn, UiUser user)
        {
            try          
            {
                // add new user to uiuser
                var Variables = new
                {
                    uuid = user.Dn, 
                    uiuser_username = user.Name,
                    email = user.Email,
                    tenant = user.Tenant.Id,
                    loginTime = DateTime.UtcNow
                };
                user.DbId = (await apiConn.SendQueryAsync<NewReturning>(AuthQueries.addUser, Variables)).ReturnIds[0].NewId;
            }
            catch (Exception exeption)
            {
                Log.WriteError("Add User Error", $"User {user.Name} could not be added to database.", exeption);
            }
        }

        private async Task updateLastLogin(APIConnection apiConn, int id) // TODO: Wrong location
        {
            try
            {
                var Variables = new
                {
                    id = id, 
                    loginTime = DateTime.UtcNow
                };
                await apiConn.SendQueryAsync<ReturnId>(FWO.ApiClient.Queries.AuthQueries.updateUserLastLogin, Variables);
            }
            catch(Exception exeption)
            {
                Log.WriteError("Update User Error", $"User {id} could not be updated in database.", exeption);
            }
        }

        private ClaimsIdentity GetClaims(UiUser user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim("x-hasura-user-id", user.DbId.ToString()));
            if (user.Dn != null && user.Dn.Length > 0)
                claimsIdentity.AddClaim(new Claim("x-hasura-uuid", user.Dn));   // UUID used for access to reports via API
                
            if (user.Tenant != null && user.Tenant.VisibleDevices != null && user.Tenant.VisibleManagements != null)
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
                if (hasuraRolesList.Contains("admin"))
                    defaultRole = "admin";
                else if (hasuraRolesList.Contains("auditor"))
                    defaultRole = "auditor";
                else if (hasuraRolesList.Contains("reporter-viewall"))
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
