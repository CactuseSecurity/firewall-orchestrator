using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Backend.Auth
{
    public class Jwt
    {
        private readonly byte[] privateKey = Encoding.UTF8.GetBytes("d105c8a1d0091ed4d2e4dba3d7bcd5e6839c852a8eaf08052dfcd7a2935b190ebdc212fc859a9998b5655ea27686539d537ba4603f3631f1298780a0e034a8c77b7de9ae03be9cf961155c969e4c031e2997d5c02617739c52e9f32755e49fcecc98d1da5e7bdd570df5faac3ce0c40d54ec5e41075e6fc37a4471e2a081ae1fb2948bc63d4075345a1c599caecc272fd64348ad4f281e860bf1bf0c35b816fa6d63382d48da08ea0a33901695ef4ad82559db39e6768560a3cc18983a68d6dd0f001df7c45605e71c06c43d5da69c4390f607616b2046c1ca3db0800e9e4ee87bdae77800b8448f2fdc682f9a3cd32739a4c9af4f0126273281906b1da05f9e");

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
                // privateKey = File.ReadAllText("../../../../../../../etc/secrets/jwt_private.key");
                privateKey = File.ReadAllBytes("/usr/local/fworch/etc/secrets/jwt_private.key");
                Console.WriteLine($"Key is {privateKey.Length} Bytes long.");
                Console.WriteLine($"Key is {Encoding.UTF8.GetString(privateKey)}");
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
                //TODO: Invalid Jwt Logging
            }
        }

        public bool Valid()
        {
            try
            {
                if (Token == null)
                    return false;

                var validationParameters = new TokenValidationParameters()
                {
                    ValidIssuer = "FWO Auth Module",
                    ValidAudience = "FWO",
                    IssuerSigningKey = new SymmetricSecurityKey(privateKey) // The same key as the one that generated the token
                };

                IPrincipal principal = Handler.ValidateToken(TokenString, validationParameters, out SecurityToken validatedToken);
            }
            catch (Exception e)
            {
                //TODO: Invalid Jwt Logging
                return false;
            }       

            return true;
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
