using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using FWO.Config;
using FWO.Logging;

namespace FWO.Middleware.Client
{
    public class JwtReader
    {
        private readonly string jwtString;
        private JwtSecurityToken jwt;

        private readonly RsaSecurityKey jwtPublicKey;

        public JwtReader(string jwtString)
        {
            // Save jwt string 
            this.jwtString = jwtString;

            // Get public key from config lib
            ConfigFile config = new ConfigFile();
            jwtPublicKey = config.JwtPublicKey;
        }

        /// <summary>
        /// checks if JWT in HTTP header conains admin role.
        /// </summary>
        /// <returns>true if JWT contains admin role, otherwise false</returns>
        public Boolean JwtContainsAdminRole()
        {
            // todo: needs to e implemented
            Claim[] claims = this.jwt.Claims.ToArray();
            if (claims.Contains(new Claim("role", "admin")))
                return true;
            else
                return false;
        }

        public bool Validate()
        {
            bool verified = true; // default ok, then set to false if any exception occurs during validation 

            try
            {
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = jwtPublicKey
                };

                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(jwtString, validationParameters, out SecurityToken validatedSecurityToken);
                jwt = (JwtSecurityToken)validatedSecurityToken;
            }

            catch (SecurityTokenExpiredException)
            {
                Log.WriteDebug("Jwt Validation", "Jwt lifetime expired.");
                verified = false;
            }
            catch (SecurityTokenInvalidSignatureException InvalidSignatureException)
            {
                Log.WriteError("Jwt Validation", $"Jwt signature could not be verified. Potential attack!", InvalidSignatureException);
                verified = false;
            }
            catch (Exception UnexpectedError)
            {
                Log.WriteError("Jwt Validation", $"Unexpected problem while trying to verify Jwt", UnexpectedError);
                verified = false;
            }

            Log.WriteDebug("Jwt Validation", "Jwt was successfully validated.");

            return verified;
        }

        public Claim[] GetClaims()
        {
            Log.WriteDebug("Claims Jwt", "Reading claims from Jwt.");
            return jwt.Claims.ToArray();
        }
    }
}
