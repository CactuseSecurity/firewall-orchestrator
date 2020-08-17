using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Backend.Auth
{
    public class Jwt
    {
        private readonly string privateKey = "J6k2eVCTXDp5b97u6gNH5GaaqHDxCmzz2wv3PRPFRsuW2UavK8LGPRauC4VSeaetKTMtVmVzAC8fh8Psvp8PFybEvpYnULHfRpM8TA2an7GFehrLLvawVJdSRqh2unCnWehhh2SJMMg5bktRRapA8EGSgQUV8TCafqdSEHNWnGXTjjsMEjUpaxcADDNZLSYPMyPSfp6qe5LMcd5S9bXH97KeeMGyZTS2U8gp3LGk2kH4J4F3fsytfpe9H9qKwgjb";

        private readonly string TokenString;
        private readonly JwtSecurityTokenHandler Handler;
        private readonly JwtSecurityToken Token;

        public Jwt(string TokenString)
        {
            this.TokenString = TokenString;
            Handler = new JwtSecurityTokenHandler();
            try 
            { 
                Token = Handler.ReadJwtToken(TokenString);
            } 
            catch (Exception)
            {
                //TODO: Invalid Jwt Logging
            }
        }

        public bool Valid()
        {
            try
            {
                var validationParameters = new TokenValidationParameters()
                {
                    ValidIssuer = "FWO Auth Module",
                    ValidAudience = "FWO",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(privateKey)) // The same key as the one that generated the token
                };

                IPrincipal principal = Handler.ValidateToken(TokenString, validationParameters, out SecurityToken validatedToken);
            }
            catch (Exception)
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
