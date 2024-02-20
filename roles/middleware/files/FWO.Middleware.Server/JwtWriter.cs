using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Data;
using FWO.Config.Api;

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
                subject = SetClaims(await uiUserHandler.HandleUiUserAtLogin(user));
            else
                subject = SetClaims(new UiUser() { Name = "", Password = "", Dn = GlobalConst.kAnonymous, Roles = new List<string> { GlobalConst.kAnonymous } });
            // adding uiuser.uiuser_id as x-hasura-user-id to JWT

            // Create JWToken
            JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken
            (
                issuer: JwtConstants.Issuer,
                audience: JwtConstants.Audience,
                subject: subject,
                notBefore: DateTime.UtcNow.AddMinutes(-1), // we currently allow for some deviation in timing of the systems
                issuedAt: DateTime.UtcNow.AddMinutes(-1),
                // Anonymous jwt is valid for ten years (does not violate security)
                expires: DateTime.UtcNow.AddMinutes(user != null ? jwtMinutesValid : 60 * 24 * 365 * 10),
                signingCredentials: new SigningCredentials(jwtPrivateKey, SecurityAlgorithms.RsaSha256)
            );

            string GeneratedToken = tokenHandler.WriteToken(token);
            if (user != null)
                Log.WriteDebug("Jwt generation", $"Generated JWT {GeneratedToken} for User {user.Name}");
            else
                Log.WriteDebug("Jwt generation", $"Generated JWT {GeneratedToken}");
            return GeneratedToken;
        }

        /// <summary>
        /// Jwt creator function used within middlewareserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for middleware-server role.</returns>
        public string CreateJWTMiddlewareServer()
        {
            return CreateJWTInternal(GlobalConst.kMiddlewareServer);
        }

        /// <summary>
        /// Jwt creator function used within middlewareserver that does not need: user, getClaims
        /// necessary because this JWT needs to be used within getClaims
        /// </summary>
        /// <returns>JWT for reporter-viewall role.</returns>
        public string CreateJWTReporterViewall()
        {
            return CreateJWTInternal(GlobalConst.kReporterViewAll);
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
            Log.WriteDebug("Jwt generation", $"Generated JWT {GeneratedToken} for {role}.");
            return GeneratedToken;
        }

        private static ClaimsIdentity SetClaims(UiUser user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.Name));
            claimsIdentity.AddClaim(new Claim("x-hasura-user-id", user.DbId.ToString()));
            if (user.Dn != null && user.Dn.Length > 0)
                claimsIdentity.AddClaim(new Claim("x-hasura-uuid", user.Dn));   // UUID used for access to reports via API
                
            if (user.Tenant != null)
            { 
                claimsIdentity.AddClaim(new Claim("x-hasura-tenant-id", user.Tenant.Id.ToString()));
                if(user.Tenant.VisibleGatewayIds != null && user.Tenant.VisibleManagementIds != null)
                {
                    // Hasura needs object {} instead of array [] notation      (TODO: Changable?)
                    claimsIdentity.AddClaim(new Claim("x-hasura-visible-managements", $"{{ {string.Join(",", user.Tenant.VisibleManagementIds)} }}"));
                    claimsIdentity.AddClaim(new Claim("x-hasura-visible-devices", $"{{ {string.Join(",", user.Tenant.VisibleGatewayIds)} }}"));
                }
            }
            claimsIdentity.AddClaim(new Claim("x-hasura-editable-owners", $"{{ {string.Join(",", user.Ownerships)} }}"));

            // we need to create an extra list because hasura only accepts an array of roles even if there is only one
            List<string> hasuraRolesList = new List<string>();

            foreach (string role in user.Roles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role)); // Frontend Roles
                hasuraRolesList.Add(role); // Hasura Roles
            }

            // add hasura roles claim as array
            claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(hasuraRolesList.ToArray()), JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array

            claimsIdentity.AddClaim(new Claim("x-hasura-default-role", GetDefaultRole(user, hasuraRolesList)));
            return claimsIdentity;
        }

        private static string GetDefaultRole(UiUser user, List<string> hasuraRolesList)
        {
            string defaultRole = "";
            if (user.Roles.Count > 0)
            {
                if (hasuraRolesList.Contains(GlobalConst.kAdmin))
                    defaultRole = GlobalConst.kAdmin;
                else if (hasuraRolesList.Contains(GlobalConst.kAuditor))
                    defaultRole = GlobalConst.kAuditor;
                else if (hasuraRolesList.Contains(GlobalConst.kFwAdmin))
                    defaultRole = GlobalConst.kFwAdmin;
                else if (hasuraRolesList.Contains(GlobalConst.kReporterViewAll))
                    defaultRole = GlobalConst.kReporterViewAll;
                else if (hasuraRolesList.Contains(GlobalConst.kReporter))
                    defaultRole = GlobalConst.kReporter;
                else if (hasuraRolesList.Contains(GlobalConst.kRecertifier))
                    defaultRole = GlobalConst.kRecertifier;
                else if (hasuraRolesList.Contains(GlobalConst.kModeller))
                    defaultRole = GlobalConst.kModeller;
                else
                    defaultRole = user.Roles[0]; // pick first role at random (todo: might need to be changed)
            }
            else
            {
                Log.WriteError("User roles", $"User {user.Name} does not have any assigned roles.");
            }
            return defaultRole;
        }
    }
}
