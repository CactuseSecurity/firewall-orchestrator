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
        private readonly string publicJWTKey; // in PEM-Format with armor
        private readonly byte[] nakedPublicKeyBytes; // without armor in binary format
        private readonly string TokenString;
        private readonly JwtSecurityTokenHandler Handler;
        private readonly JwtSecurityToken Token;
        private readonly RSACryptoServiceProvider rsa;

        public Jwt(string TokenString)
        {   // Get public key from file
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
            // and import it into RSACryptoObject
            try
            {
                this.rsa = new RSACryptoServiceProvider();
                this.rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(AuthClient.ExtractKeyFromPemAsString(publicJWTKey, false)), out _);
            }
            catch (Exception e)
            {
                ConsoleColor StandardConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteAsync($"Error while trying to import public key into RSACryptoServiceProvider : \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
                Console.Out.WriteAsync($"Using fallback key! \n");
                Console.ForegroundColor = StandardConsoleColor;
            }
            // store Jwt token string and import it into JwtToken object
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
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = new RsaSecurityKey(this.rsa)
                };
                SecurityToken validatedSecurityToken = null;
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(TokenString, validationParameters, out validatedSecurityToken);
                JwtSecurityToken validatedJwt = validatedSecurityToken as JwtSecurityToken;
            }
            catch (SecurityTokenInvalidSignatureException) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT signature could not be verified.");
                verified = false;
            }
            catch (SecurityTokenExpiredException) {
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
