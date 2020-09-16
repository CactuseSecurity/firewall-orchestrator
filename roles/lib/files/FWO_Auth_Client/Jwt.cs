using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FWO.Auth.Client
{
    public class Jwt
    {
        private string jwt_generator_public_key_file = "../../../etc/secrets/jwt_public_key.pem";
        private readonly string publicJWTKey;
        private readonly string TokenString;
        private readonly JwtSecurityTokenHandler Handler;
        private readonly JwtSecurityToken Token;

        public Jwt(string TokenString)
        {   // Get private key from file
            try
            {
                publicJWTKey = File.ReadAllText(jwt_generator_public_key_file).TrimEnd();
            }
            catch (Exception e)
            {
                ConsoleColor StandardConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteAsync($"Error while trying to read public key : \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
                Console.Out.WriteAsync($"Using fallback key! \n");
                Console.ForegroundColor = StandardConsoleColor;
            }

            this.TokenString = TokenString;
            Handler = new JwtSecurityTokenHandler();
            try 
            { 
                if (TokenString != null)
                    Token = Handler.ReadJwtToken(TokenString);
            } 
            catch (Exception e)
            {
                Console.Out.WriteAsync($"Auth_Client:: error while adding (not validating) JWT: \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
            }
        } 

        static private byte[] FromBase64Url(string base64Url)
        {
            string padded = base64Url.Length % 4 == 0 ? base64Url : base64Url + "====".Substring(base64Url.Length % 4);
            return Convert.FromBase64String(padded.Replace("_", "/").Replace("-", "+"));
        }

        public bool Valid()
        {
            if (Token == null)
                return false;
            string[] tokenParts = this.TokenString.Split('.');
            if (tokenParts[2] == null || tokenParts[2] == String.Empty)
                return false;
            bool isPrivateKey = false;
            bool verified = true;
            
            try
            { // source: https://stackoverflow.com/questions/34403823/verifying-jwt-signed-with-the-rs256-algorithm-using-public-key-in-c-sharp (#29)                
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(AuthClient.ExtractKeyFromPemAsString(publicJWTKey, isPrivateKey)), out _);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa)
                };
                SecurityToken validatedSecurityToken = null;
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(TokenString, validationParameters, out validatedSecurityToken);
                JwtSecurityToken validatedJwt = validatedSecurityToken as JwtSecurityToken;
            }
            catch (SecurityTokenInvalidSignatureException SignFault) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT signature could not be verified.");
                verified = false;
            }
            catch (SecurityTokenExpiredException Expiry) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT expired.");
                verified = false;
            }
            catch (Exception CatchRest) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: unspecified problem with JWT: {CatchRest.Message}");
                verified = false;
            }
#if DEBUG
            if (verified) 
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT signature validated.");
#endif
            return verified;
        }            

        public Claim[] GetClaims()
        {
            try
            {
                return Token.Claims.ToArray();
            }
            catch (Exception)
            {
                //This should never happen
            }

            return new Claim[0];
        }
    }
}
