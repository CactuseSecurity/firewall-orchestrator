using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FWO.Auth.Client
{
    public class Jwt
    {
        private string jwt_generator_private_key_file = "/usr/local/fworch/etc/secrets/jwt_public_key.pem";
        private readonly string publicJWTKey = "8f4ce02dabb2a4ffdb2137802b82d1283f297d959604451fd7b7287aa307dd298668cd68a432434d85f9bcff207311a833dd5b522870baf457c565c7a716e7eaf6be9a32bd9cd5420a0ebaa9bace623b54c262dcdf35debdb5388490008b9bc61facfd237c1c7058f5287881a37492f523992a2a120a497771954daf27666de2461a63117c8347fe760464e3a58b3a5151af56a0375c8b34921101c91425b65097fc69049f85589a58bb5e5570139c98d3edb179a400b3d142a30e32d1c8e9bbdb90d799fb81b4fa6fb7751acfb3529c7af022590cbb845a8390b906f725f079967b269cff8d2e6c8dbcc561b37c4bdd1928c662b79f42fe56fe108a0cf21e08";
        private readonly string TokenString;
        private readonly JwtSecurityTokenHandler Handler;
        private readonly JwtSecurityToken Token;

        public Jwt(string TokenString)
        {
            // Get private key from
            // absolute path: "fworch_home/etc/secrets/jwt_private.key"
            // relative path:  "../../../etc/secrets"
            try
            {
                // privateJWTKey = File.ReadAllText("../../../../../../../etc/secrets/jwt_private.key");
                publicJWTKey = File.ReadAllText(jwt_generator_private_key_file).TrimEnd();
            }
            catch (Exception e)
            {
                ConsoleColor StandardConsoleColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Red;

                Console.Out.WriteAsync($"Error while trying to read private key : \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
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
                Console.Out.WriteAsync($"Auth_Client:: error while reading JWT: \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
            }
        } 

        static private byte[] FromBase64Url(string base64Url)
        {
            // return Convert.FromBase64String(base64Url);
            string padded = base64Url.Length % 4 == 0 ? base64Url : base64Url + "====".Substring(base64Url.Length % 4);
            // return Convert.FromBase64String(padded);
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
            int bytesRead = 0;
            bool verified = true;
            string pubKey = AuthClient.ExtractKeyFromPemAsString(publicJWTKey, isPrivateKey);
            RsaSecurityKey pubKeyRsa = AuthClient.ExtractKeyFromPem(publicJWTKey, false);
            
            try
            { // source: https://stackoverflow.com/questions/34403823/verifying-jwt-signed-with-the-rs256-algorithm-using-public-key-in-c-sharp (#29)                
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pubKey), out bytesRead);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa)
                };
                // invalidate Token
                string faultyTokenString = TokenString + "fault";
                SecurityToken validatedSecurityToken = null;
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(TokenString, validationParameters, out validatedSecurityToken);
                // handler.ValidateToken(faultyTokenString, validationParameters, out validatedSecurityToken); // todo: write tests
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
