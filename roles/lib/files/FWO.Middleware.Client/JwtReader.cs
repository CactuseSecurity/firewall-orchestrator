using FWO.Config.File;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FWO.Middleware.Client
{
    public class JwtReader
    {
        private readonly string jwtString;
        private JsonWebToken? jwt;

        private readonly RsaSecurityKey jwtPublicKey;
        private readonly string JwtValidation = "Jwt Validation";
        private readonly string JwtNotValidated = "Jwt was not validated yet.";


        public JwtReader(string jwtString)
        {
            // Save jwt string 
            this.jwtString = jwtString;

            // Get public key from config lib
            jwtPublicKey = ConfigFile.JwtPublicKey ?? throw new ArgumentException("Jwt public key could not be read form config file.");
        }

        /// <summary>
        /// Checks if JWT in HTTP header contains role.
        /// </summary>
        /// <param name="roleName">Role name to check.</param>
        /// <returns>True if JWT contains specified role, otherwise false.</returns>
        public bool ContainsRole(string roleName)
        {
            Log.WriteDebug($"{roleName} Role Jwt", "Checking Jwt for admin role.");

            if (jwt == null)
                throw new ArgumentException(nameof(jwt), JwtNotValidated);

            return jwt.Claims.FirstOrDefault(claim => claim.Type == "role" && claim.Value == roleName) != null;
        }

        /// <summary>
        /// Checks if JWT in HTTP header contains role in x-hasura-allowed-roles.
        /// </summary>
        /// <param name="roleName">Role name to check.</param>
        /// <returns>True if JWT contains specified role in x-hasura-allowed-roles, otherwise false.</returns>
        public bool ContainsAllowedRole(string roleName)
        {
            Log.WriteDebug($"{roleName} Role Jwt", "Checking Jwt for allowed role.");

            if (jwt == null)
                throw new ArgumentException(nameof(jwt), JwtNotValidated);

            return jwt.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-allowed-roles" && claim.Value == roleName) != null;
        }

        public async Task<JwtValidationResult> ValidateToken()
        {
            try
            {
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateLifetime = true,
                    ValidateAudience = true,
                    ValidAudiences = [FWO.Basics.JwtConstants.Audience],
                    ValidateIssuer = true,
                    ValidIssuer = FWO.Basics.JwtConstants.Issuer,
                    IssuerSigningKey = jwtPublicKey,
                };

                JsonWebTokenHandler handler = new();
                TokenValidationResult tokenValidationResult = await handler.ValidateTokenAsync(jwtString, validationParameters);

                jwt = tokenValidationResult.SecurityToken as JsonWebToken;

                if (tokenValidationResult.IsValid)
                {
                    return new JwtValidationResult
                    {
                        Status = JwtValidationStatus.Success,
                        Token = jwt
                    };
                }

                if (tokenValidationResult.Exception is SecurityTokenExpiredException)
                {                    
                    return new JwtValidationResult
                    {
                        Status = JwtValidationStatus.Expired
                    };
                }

                Log.WriteError(JwtValidation, "Jwt validation failed.", tokenValidationResult.Exception);

                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Invalid
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Expired
                };
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                Log.WriteAudit(JwtValidation, BuildAuditText(jwtString, "Jwt signature could not be verified."));

                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Invalid
                };
            }
            catch (SecurityTokenInvalidAudienceException)
            {
                Log.WriteAudit(JwtValidation, BuildAuditText(jwtString, "Jwt audience incorrect."));

                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Invalid
                };
            }
            catch (SecurityTokenInvalidIssuerException)
            {
                Log.WriteAudit(JwtValidation, BuildAuditText(jwtString, "Jwt issuer incorrect."));

                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Invalid
                };
            }
            catch (Exception UnexpectedError)
            {
                Log.WriteError(JwtValidation, "Unexpected problem while trying to verify Jwt.", UnexpectedError);

                return new JwtValidationResult
                {
                    Status = JwtValidationStatus.Invalid
                };
            }
        }

        /// <summary>
        /// Builds the audit text for an issued access and optional refresh token pair.
        /// </summary>
        /// <param name="jwt">Jwt string.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        /// <returns>Audit message text containing jti and expiry information.</returns>
        private static string BuildAuditText(string jwt, string actionText)
        {
            JsonWebTokenHandler handler = new();
            JsonWebToken accessToken = handler.ReadJsonWebToken(jwt);

            return $"{actionText} Potential attack: access_jti={accessToken.Id}, access_expires={accessToken.ValidTo.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}";
        }

        public Claim[] GetClaims()
        {
            Log.WriteDebug("Claims Jwt", "Reading claims from Jwt.");
            if (jwt == null)
                throw new ArgumentException(nameof(jwt), JwtNotValidated);

            return jwt.Claims.ToArray();
        }

        public string GetRole()
        {
            if (jwt == null)
                throw new ArgumentException(nameof(jwt), JwtNotValidated);
            return jwt.Claims.FirstOrDefault(claim => claim.Type == "role")?.Value ?? "";
        }
    }
}
