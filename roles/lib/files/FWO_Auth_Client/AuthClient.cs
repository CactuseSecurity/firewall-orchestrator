using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace FWO.Auth.Client
{
    public class AuthClient
    {
        private readonly HttpClient HttpClient;
        private readonly string AuthServerUri;

        public AuthClient(string AuthServerUri)
        {
            this.AuthServerUri = AuthServerUri;
            HttpClient = new HttpClient();
        }

        public async Task<string> GetJWT(string Username, string Password)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { "Username", Username },
                { "Password", Password }
            };

            string ParametersJson = JsonSerializer.Serialize(Parameters);

            StringContent content = new StringContent(ParametersJson);           
            Console.WriteLine("Sending GetJWT Request...");
            HttpResponseMessage response = await HttpClient.PostAsync(AuthServerUri + "jwt", content);
            return await response.Content.ReadAsStringAsync();
        }

        public static RsaSecurityKey ExtractKeyFromPem(string RawKey, bool isPrivateKey)
        {
            string keyText = ExtractKeyFromPemAsString(RawKey, isPrivateKey);
            RsaSecurityKey rsaKey = null;
           
            try
            {
                byte[] keyBytes = Convert.FromBase64String(keyText);
               // creating the RSA key 
                RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
                if (isPrivateKey)
// debian 10:
                    provider.ImportPkcs8PrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
// ubuntu 20.04:
//                    provider.ImportRSAPrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                else
                    provider.ImportSubjectPublicKeyInfo(new ReadOnlySpan<byte>(keyBytes), out _);

                rsaKey = new RsaSecurityKey(provider);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
            }
            return rsaKey;
        }
        public static string ExtractKeyFromPemAsString(string rawKey, bool isPrivateKey)
        {
            string keyText = null;
            Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString rawKey={rawKey}");
            try
            {
                // removing everything but the base64 encoded key string from private key PEM 
                List<string> lines = new List<string>(rawKey.Split('\n'));
                keyText = String.Join('\n', lines.GetRange(1,lines.Count-2).ToArray());
                keyText = keyText.Replace("\n", "");    // remove line breaks
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
            }
            Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString keyText={keyText}");
            return keyText;
        }
    }
}
