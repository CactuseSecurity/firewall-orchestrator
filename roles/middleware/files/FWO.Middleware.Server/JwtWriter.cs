using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Data;
using Microsoft.IdentityModel.JsonWebTokens;

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
			JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();
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

			JwtSecurityTokenHandler tokenHandler = new ();

			UiUserHandler uiUserHandler = new (CreateJWTMiddlewareServer());
			// if lifetime was speciefied use it, otherwise use standard lifetime
			int jwtMinutesValid = (int)(lifetime?.TotalMinutes ?? await uiUserHandler.GetExpirationTime());

			ClaimsIdentity subject;
			if (user != null)
				subject = SetClaims(await uiUserHandler.HandleUiUserAtLogin(user));
			else
				subject = SetClaims(new UiUser() { Name = "", Password = "", Dn = Roles.Anonymous, Roles = [Roles.Anonymous] });
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
				Log.WriteDebug("Jwt generation", $"Generated JWT {token.RawData} for User {user.Name}");
			else
				Log.WriteDebug("Jwt generation", $"Generated JWT {token.RawData}");
			return GeneratedToken;
		}

		/// <summary>
		/// Jwt creator function used within middlewareserver that does not need: user, getClaims
		/// necessary because this JWT needs to be used within getClaims
		/// </summary>
		/// <returns>JWT for middleware-server role.</returns>
		public string CreateJWTMiddlewareServer()
		{
			return CreateJWTInternal(Roles.MiddlewareServer);
		}

		/// <summary>
		/// Jwt creator function used within middlewareserver that does not need: user, getClaims
		/// necessary because this JWT needs to be used within getClaims
		/// </summary>
		/// <returns>JWT for reporter-viewall role.</returns>
		public string CreateJWTReporterViewall()
		{
			return CreateJWTInternal(Roles.ReporterViewAll);
		}

		private string CreateJWTInternal(string role)
		{
			JwtSecurityTokenHandler tokenHandler = new ();
			ClaimsIdentity subject = new ();
			subject.AddClaim(new Claim("unique_name", role));
			subject.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(new string[] { role }), System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.JsonArray));
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
			ClaimsIdentity claimsIdentity = new ();
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
			AddRoleClaims(claimsIdentity, user);
			return claimsIdentity;
		}

		private static void AddRoleClaims(ClaimsIdentity claimsIdentity, UiUser user)
		{
			// we need to create an extra list because hasura only accepts an array of roles even if there is only one
			List<string> hasuraRolesList = [];
			foreach (string role in user.Roles)
			{
				claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role)); // Frontend Roles
				hasuraRolesList.Add(role); // Hasura Roles
			}
			// add hasura roles claim as array
			claimsIdentity.AddClaim(new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(hasuraRolesList.ToArray()),  System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.JsonArray)); // Convert Hasura Roles to Array
			claimsIdentity.AddClaim(new Claim("x-hasura-default-role", GetDefaultRole(user, hasuraRolesList)));
		}

		private static string GetDefaultRole(UiUser user, List<string> hasuraRolesList)
		{
			string defaultRole = "";
			if (user.Roles.Count > 0)
			{
				if (hasuraRolesList.Contains(Roles.Admin))
					defaultRole = Roles.Admin;
				else if (hasuraRolesList.Contains(Roles.Auditor))
					defaultRole = Roles.Auditor;
				else if (hasuraRolesList.Contains(Roles.FwAdmin))
					defaultRole = Roles.FwAdmin;
				else if (hasuraRolesList.Contains(Roles.ReporterViewAll))
					defaultRole = Roles.ReporterViewAll;
				else if (hasuraRolesList.Contains(Roles.Reporter))
					defaultRole = Roles.Reporter;
				else if (hasuraRolesList.Contains(Roles.Recertifier))
					defaultRole = Roles.Recertifier;
				else if (hasuraRolesList.Contains(Roles.Modeller))
					defaultRole = Roles.Modeller;
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
