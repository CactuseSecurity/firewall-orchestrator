using FWO.Basics;
using Microsoft.IdentityModel.Tokens;

namespace FWO.Config.File
{
    /// <summary>
    /// Validation of the JWTs issued by the middleware server, for the services accepting them on behalf of FWO users,
    /// such as the AI MCP server and the AI harness.
    /// </summary>
    public static class JwtValidation
    {
        private static readonly List<string> kValidAudiences = [JwtConstants.Audience];
        private static readonly List<string> kValidIssuers = [JwtConstants.Issuer];

        /// <summary>
        /// Creates parameters that accept only signed, unexpired tokens of the middleware server's issuer and audience,
        /// signed with the given key. The user name and the roles are read from the claims as written in the token
        /// (<see cref="JwtClaimTypes.kName"/>, <see cref="JwtClaimTypes.kRole"/>), so the inbound claims must not be
        /// mapped (JwtBearerOptions.MapInboundClaims = false).
        /// </summary>
        /// <param name="jwtPublicKey">Public key of the middleware server, see <see cref="ConfigFile.JwtPublicKey"/></param>
        /// <returns>New validation parameters for each call.</returns>
        public static TokenValidationParameters CreateParameters(SecurityKey jwtPublicKey)
        {
            ArgumentNullException.ThrowIfNull(jwtPublicKey);
            return new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateAudience = true,
                ValidAudiences = kValidAudiences,
                ValidateIssuer = true,
                ValidIssuers = kValidIssuers,
                ValidateLifetime = true,
                NameClaimType = JwtClaimTypes.kName,
                RoleClaimType = JwtClaimTypes.kRole,
                IssuerSigningKey = jwtPublicKey
            };
        }
    }
}
