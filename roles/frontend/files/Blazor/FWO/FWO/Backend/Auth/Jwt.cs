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
        private readonly byte[] privateKey = Encoding.UTF8.GetBytes("d76d62deca81333fbb5ee8435063b72ee66887ecbc66163a0367d05325aea4b0");

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
