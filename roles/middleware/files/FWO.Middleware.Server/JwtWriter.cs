using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Data;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class for jwt creation
	/// </summary>
    public class JwtWriter
    {
        private readonly RsaSecurityKey jwtPrivateKey;

		/// <summary>
		/// Constructor needing the private key
		/// </summary>
        public JwtWriter(RsaSecurityKey jwtPrivateKey)
        {
            this.jwtPrivateKey = jwtPrivateKey;
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        }

		/// <summary>
		/// create jwt for given user
		/// </summary>
		/// <returns>generated token</returns>
        public async Task<string> CreateJWT(UiUser? user = null, TimeSpan? lifetime = null)
        {
            if (user != null)
                Log.WriteDebug("Jwt generation", $"Generating JWT for user {user.Name} ...");
            else
                Log.WriteDebug("Jwt generation", "Generating empty JWT (startup)");

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            UiUserHandler uiUserHandler = new UiUserHandler(CreateJWTMiddlewareServer());
            // if lifetime was speciefied use it, otherwise use standard lifetime
            int jwtMinutesValid = (int)(lifetime?.TotalMinutes ?? await uiUserHandler.GetExpirationTime());

            ClaimsIdentity subject;
            if (user != null)
                subject = GetClaims(await uiUserHandler.HandleUiUserAtLogin(user));
            else
                subject = GetClaims(new UiUser() { Name = "", Password = "", Dn = "anonymous", Roles = new List<string> { "anonymous" } });
            // adding uiuser.uiuser_id as x-hasura-user-id to JWT

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: JwtConstants.Issuer,
                audience: JwtConstants.Audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(jwtMinutesValid),
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
            return CreateJWTInternal("middleware-server");
        }

        /// <summary>
        /// Jwt creator function used within middlewareserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for reporter-viewall role.</returns>
        public string CreateJWTReporterViewall()
        {
            return CreateJWTInternal("reporter-viewall");
        }

        private string CreateJWTInternal(string role)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity subject = new ClaimsIdentity();
            subject.AddClaim(new Claim("unique_name", role));
            subject.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(new string[] { role }), JsonClaimValueTypes.JsonArray));
            subject.AddClaim(new Claim("x-hasura-default-role", role));

            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: JwtConstants.Issuer,
                audience: JwtConstants.Audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddYears(200),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );
            string GeneratedToken = tokenHandler.WriteToken(token);
            Log.WriteInfo("Jwt generation", $"Generated JWT {GeneratedToken} for {role}.");
            return GeneratedToken;
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

            // we need to create an extra list beacause hasura only accepts an array of roles even if there is only one
            List<string> hasuraRolesList = new List<string>();

            foreach (string role in user.Roles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role)); // Frontend Roles
                hasuraRolesList.Add(role); // Hasura Roles
            }

            // add hasura roles claim as array
            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(hasuraRolesList.ToArray()), JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array

            // deciding on default-role
            string defaultRole = "";
            if (user.Roles.Count > 0)
            {
                if (hasuraRolesList.Contains("admin"))
                    defaultRole = "admin";
                else if (hasuraRolesList.Contains("auditor"))
                    defaultRole = "auditor";
                else if (hasuraRolesList.Contains("fw-admin"))
                    defaultRole = "fw-admin";
                else if (hasuraRolesList.Contains("reporter-viewall"))
                    defaultRole = "reporter-viewall";
                else if (hasuraRolesList.Contains("reporter"))
                    defaultRole = "reporter";
                else
                    defaultRole = user.Roles[0]; // pick first role at random (todo: might need to be changed)
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
