using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Claims;
using FWO.Config.File;
using FWO.Logging;

namespace FWO.Middleware.Client
{
    public class JwtReader
    {
        private readonly string jwtString;
        private JwtSecurityToken? jwt;

        private readonly RsaSecurityKey jwtPublicKey;

        public JwtReader(string jwtString)
        {
            // Save jwt string 
            this.jwtString = jwtString;

            // Get public key from config lib
            ConfigFile config = new ConfigFile();
            jwtPublicKey = ConfigFile.JwtPublicKey ?? throw new Exception("Jwt public key could not be read form config file.");
        }

        /// <summary>
        /// checks if JWT in HTTP header conains admin role.
        /// </summary>
        /// <returns>true if JWT contains admin role, otherwise false</returns>
        public bool JwtContainsAdminRole()
        {
            Log.WriteDebug("Admin Role Jwt", "Checking Jwt for admin role.");

            if (jwt == null)
                throw new ArgumentNullException(nameof(jwt), "Jwt was not validated yet.");

            return jwt.Claims.FirstOrDefault(claim => claim.Type == "role" && claim.Value == "admin") != null;
        }

        public bool JwtContainsAuditorRole()
        {
            Log.WriteDebug("Admin Role Jwt", "Checking Jwt for auditor role.");

            if (jwt == null)
                throw new ArgumentNullException(nameof(jwt), "Jwt was not validated yet.");

            return jwt.Claims.FirstOrDefault(claim => claim.Type == "role" && claim.Value == "auditor") != null;
        }

        public bool Validate()
        {
            try
            {
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidAudience = JwtConstants.Audience,
                    ValidIssuer = JwtConstants.Issuer,
                    IssuerSigningKey = jwtPublicKey
                };

                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(jwtString, validationParameters, out SecurityToken validatedSecurityToken);
                jwt = (JwtSecurityToken)validatedSecurityToken;
                Log.WriteDebug("Jwt Validation", "Jwt was successfully validated.");
                return true;
            }

            catch (SecurityTokenExpiredException)
            {
                Log.WriteDebug("Jwt Validation", "Jwt lifetime expired.");
                return false;
            }
            catch (SecurityTokenInvalidSignatureException InvalidSignatureException)
            {
                Log.WriteError("Jwt Validation", $"Jwt signature could not be verified. Potential attack!", InvalidSignatureException);
                return false;
            }
            catch (SecurityTokenInvalidAudienceException InvalidAudienceException)
            {
                Log.WriteError("Jwt Validation", $"Jwt audience incorrect.", InvalidAudienceException);
                return false;
            }
            catch (SecurityTokenInvalidIssuerException InvalidIssuerException)
            {
                Log.WriteError("Jwt Validation", $"Jwt issuer incorrect.", InvalidIssuerException);
                return false;
            }
            catch (Exception UnexpectedError)
            {
                Log.WriteError("Jwt Validation", $"Unexpected problem while trying to verify Jwt", UnexpectedError);
                return false;
            }
        }

        public Claim[] GetClaims()
        {
            Log.WriteDebug("Claims Jwt", "Reading claims from Jwt.");
            if (jwt == null)
                throw new ArgumentNullException(nameof(jwt), "Jwt was not validated yet.");

            return jwt.Claims.ToArray();
        }

        public TimeSpan TimeUntilExpiry()
        {
            if (jwt == null)
                throw new ArgumentNullException(nameof(jwt), "Jwt was not validated yet.");

            return jwt.ValidTo - DateTime.UtcNow;
        }
    }
}
