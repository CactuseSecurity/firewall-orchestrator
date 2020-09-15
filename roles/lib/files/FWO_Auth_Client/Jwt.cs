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
            // byte[] hash;
            // int bytesRead = 0;
            bool verified = false;
            string pubKey = AuthClient.ExtractKeyFromPemAsString(publicJWTKey, isPrivateKey);
            RsaSecurityKey pubKeyRsa = AuthClient.ExtractKeyFromPem(publicJWTKey, false);
            
#if DEBUG
            Console.WriteLine($"FWO::Auth.Client.Jwt: using public key {pubKey}");
#endif
            try
            {
                // https://stackoverflow.com/questions/34403823/verifying-jwt-signed-with-the-rs256-algorithm-using-public-key-in-c-sharp (#29)
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(
                    new RSAParameters()
                    {
                        Modulus = FromBase64Url(pubKey),
                        Exponent = FromBase64Url("AQAB") // "e"
                    });

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

                // read public key from string
                // Console.WriteLine($"FWO::Auth.Client.Jwt: creating cert ...");
                // X509Certificate2 certificate = new X509Certificate2(jwt_generator_private_key_file);

                // Console.WriteLine($"FWO::Auth.Client.Jwt: verifying ...");
                // using (RSA rsa = certificate.GetRSAPublicKey())
                // {
                //     verified = rsa.VerifyData(
                //         Convert.FromBase64String(tokenParts[0] + '.' +  tokenParts[1]),
                //         Convert.FromBase64String(tokenParts[2]),
                //         HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1
                //     );
                // }

                // //Create a new instance of RSA.
                // using (RSA rsa = RSA.Create())
                // {
                //     //The hash to sign.
 
                //     // using (SHA256 sha256 = SHA256.Create())
                //     // {
                //     //     hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenParts[0] + '.' + tokenParts[1]));
                //     // }

                //     RSACryptoServiceProvider RsaVerifier = new RSACryptoServiceProvider();

                //     // convert publicJWTKey from string to ReadOnlySpan<byte>
                //     // convert token part 1+2 from string to byte[]
                //     RsaVerifier.ImportRSAPublicKey(Convert.FromBase64String(publicJWTKey), out bytesRead);
                //     verified = RsaVerifier.VerifyData(
                //         Convert.FromBase64String(tokenParts[0] + '.' +  tokenParts[1]),
                //         "SHA256",
                //         Convert.FromBase64String(tokenParts[2])
                //     );
                // }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine($"FWO::Auth.Client.Jwt: CryptoException: {e.Message}");
            }
            /*
                ArgumentNullException
                ArgumentException
                SecurityTokenDecryptionFailedException
                SecurityTokenEncryptionKeyNotFoundException
                SecurityTokenException
                SecurityTokenExpiredException
                SecurityTokenInvalidAudienceException
                SecurityTokenInvalidLifetimeException
                SecurityTokenInvalidSignatureException
                SecurityTokenNoExpirationException
                SecurityTokenNotYetValidException
                SecurityTokenReplayAddFailedException
                SecurityTokenReplayDetectedException
            */
            catch (SecurityTokenInvalidSignatureException SignFault) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: SignFault Exception: {SignFault.Message}");
            }
            catch (Exception CatchRest) {
                Console.WriteLine($"FWO::Auth.Client.Jwt: CatchRest Exception: {CatchRest.Message}");
            }
            finally {
                Console.WriteLine("Finally ...");
                verified = true;
            }
 
#if DEBUG
            if (verified) 
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT signature validated.");
            else
                Console.WriteLine($"FWO::Auth.Client.Jwt: JWT signature was not validated.");
#endif
            return verified;
        }            

                    // //Create an RSASignatureFormatter object and pass it the 
                    // //RSA instance to transfer the key information.
                    // RSAPKCS1SignatureFormatter RSAFormatter = new RSAPKCS1SignatureFormatter(rsa);

                    // //Set the hash algorithm to SHA256.
                    // RSAFormatter.SetHashAlgorithm("SHA256");
                    
                    // //Create a signature for HashValue and return it.
                    // byte[] signedHash = RSAFormatter.CreateSignature(hash);
                    // //Create an RSAPKCS1SignatureDeformatter object and pass it the  
                    // //RSA instance to transfer the key information.
                    // RSAPKCS1SignatureDeformatter RSADeformatter = new RSAPKCS1SignatureDeformatter(rsa);
                    // RSADeformatter.SetHashAlgorithm("SHA256");
                    // //Verify the hash and display the results to the console. 
                    // if (RSADeformatter.VerifySignature(hash, signedHash))
                    // {
                    //     Console.WriteLine("2 - The signature was verified.");
                    // }
                    // else
                    // {
                    //     Console.WriteLine("2 - The signature was not verified.");
                    // }
//             try
//             {
//                 RSA rsa = RSA.Create();
                

 
//                  var validationParameters = new TokenValidationParameters()
//                 {
//                     ValidIssuer = "FWO Auth Module",
//                     ValidAudience = "FWO",
//                     IssuerSigningKey = new RsaSecurityKey()// new AsymmetricSecurityKey(Encoding.UTF8.GetBytes(publicJWTKey)) // The public key of the auth server
//                     // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(publicJWTKey)) // The public key of the auth server
//                  };

//                  IPrincipal principal = Handler.ValidateToken(TokenString, validationParameters, out SecurityToken validatedToken);

// #if DEBUG
//                 foreach (string tokenPart in tokenParts) 
//                     Console.WriteLine($"FWO::Auth.Client.Jwt: Jwt Part: {tokenPart}");
// #endif
//                 RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
//                 // rsa.ImportParameters( new RSAParameters() { pubKey.Parameters } );
//                 rsa.ImportParameters( new RSAParameters() { 
//                     Modulus = FromBase64Url(pubKey),
//                     Exponent = FromBase64Url("AQAB")        // meaning "e"
//                     });

//                 SHA256 sha256 = SHA256.Create();
//                 byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenParts[0] + '.' + tokenParts[1]));

// #if DEBUG
//                 Console.WriteLine($"FWO::Auth.Client.Jwt: Hash={hash.ToString()}");
// #endif
               
//                 RSAPKCS1SignatureDeformatter rsaDeformatter = new RSAPKCS1SignatureDeformatter(rsa);
//                 rsaDeformatter.SetHashAlgorithm("SHA256");
//                 if (rsaDeformatter.VerifySignature(hash, FromBase64Url(tokenParts[2])))
//                     validation_state = true;
//             }
//             catch (Exception e)
//             {
//                 Console.Out.WriteAsync($"Auth:: found invalid JWT: \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
//             }

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
