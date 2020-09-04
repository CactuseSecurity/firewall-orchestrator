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
        private readonly string privateJWTKey = "8f4ce02dabb2a4ffdb2137802b82d1283f297d959604451fd7b7287aa307dd298668cd68a432434d85f9bcff207311a833dd5b522870baf457c565c7a716e7eaf6be9a32bd9cd5420a0ebaa9bace623b54c262dcdf35debdb5388490008b9bc61facfd237c1c7058f5287881a37492f523992a2a120a497771954daf27666de2461a63117c8347fe760464e3a58b3a5151af56a0375c8b34921101c91425b65097fc69049f85589a58bb5e5570139c98d3edb179a400b3d142a30e32d1c8e9bbdb90d799fb81b4fa6fb7751acfb3529c7af022590cbb845a8390b906f725f079967b269cff8d2e6c8dbcc561b37c4bdd1928c662b79f42fe56fe108a0cf21e08";
        //private readonly string privateJWTKey = "769d910a91f5ccce38cecf976d04c47bb8906160c359936e3c321ee0f3d496009190a4ddb81d79934d15291d2e0b0ecd5f43122acb4deea0d5f52d657a44d9aa50dc6145b969d0f6ed7ab9f161f80b7dfcb158104d3097f17b487190ac18d71f3b1fa92c2862f238360ae955ab626b278763c7ae889350624532ccc07fd7ada256af826fcf6f8df91f400aca67c267afb4dc6df689a2c20f280d85cb99d9cb44615d96ecdb4a215e69403b2f1c350112b6cb8333c87b59e98f16f2748bab1ca74ca808cf1c7bf320c4914c767e40e0bc4dffef05c6b28794a73d67ee09ef9b55be2ec0d2b5e0e5a548582ae095a36245c433371a560c7e4cf0011dfd657a708e";

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
                privateJWTKey = File.ReadAllText("/usr/local/fworch/etc/secrets/jwt_private.key").TrimEnd();
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(privateJWTKey)) // The same key as the one that generated the token
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
