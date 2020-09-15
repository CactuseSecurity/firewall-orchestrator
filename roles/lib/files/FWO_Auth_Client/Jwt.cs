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
using System.Threading.Tasks;

namespace FWO.Auth.Client
{
    public class Jwt
    {
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
                publicJWTKey = File.ReadAllText("/usr/local/fworch/etc/secrets/jwt_public.key").TrimEnd();
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
            string padded = base64Url.Length % 4 == 0
                ? base64Url : base64Url + "====".Substring(base64Url.Length % 4);
            string base64 = padded.Replace("_", "/")
                                .Replace("-", "+");
            return Convert.FromBase64String(base64);
        }

        public bool Valid()
        {
            bool isPrivateKey = false;
            string pubKey = AuthClient.ExtractKeyFromPemAsString(publicJWTKey, isPrivateKey);
            try
            {
                if (Token == null)
                    return false;

                // var validationParameters = new TokenValidationParameters()
                // {
                //     ValidIssuer = "FWO Auth Module",
                //     ValidAudience = "FWO",
                //     IssuerSigningKey = pubKey // The public key of the auth server
                //     // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(publicJWTKey)) // The public key of the auth server
                // };

                //         IPrincipal principal = Handler.ValidateToken(TokenString, validationParameters, out SecurityToken validatedToken);

                string[] tokenParts = this.TokenString.Split('.');
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                // rsa.ImportParameters( new RSAParameters() { pubKey.Parameters } );
                rsa.ImportParameters( new RSAParameters() { 
                    Modulus = FromBase64Url(pubKey),
                    Exponent = FromBase64Url("AQAB")
                    });

                SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenParts[0] + '.' + tokenParts[1]));

                RSAPKCS1SignatureDeformatter rsaDeformatter = new RSAPKCS1SignatureDeformatter(rsa);
                rsaDeformatter.SetHashAlgorithm("SHA256");
                if (rsaDeformatter.VerifySignature(hash, FromBase64Url(tokenParts[2])))
                    return true;
                else
                    return false;

            }
            catch (Exception e)
            {
                Console.Out.WriteAsync($"Auth:: found invalid JWT: \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
                return false;
            }       
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
