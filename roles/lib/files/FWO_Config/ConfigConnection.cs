using FWO.Api;
using FWO.Auth.Client;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FWO.Config
{
    public class ConfigConnection
    {
        /// <summary>
        /// Path to config files
        /// </summary>
        private const string configPath = ""; //TODO: Add path to config (absolute)

        private readonly AuthClient authClient;

        private readonly APIConnection apiConnection;

        private Dictionary<string, string> configData;

        public string this[string configKey]
        {
            get
            {
                return configData[configKey];
            }

            set
            {
                configData[configKey] = value;
            }
        }

        public ConfigConnection()
        {
            #region Config File

            // Read config as yaml from file
            string yamlConfig = File.ReadAllText(configPath).TrimEnd();

            // Create yaml deserializer
            IDeserializer yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            // Deserialize yaml config to dictionary
            configData = yamlDeserializer.Deserialize<Dictionary<string, string>>(yamlConfig);

            #endregion

            #region Config Api
            
            authClient = new AuthClient(configData["auth_uri"]);

            apiConnection = new APIConnection(configData["api_uri"]);
            apiConnection.SetAuthHeader("");

            #endregion
        }

        public static RsaSecurityKey ExtractKeyFromPem(string RawKey, bool isPrivateKey)
        {
            bool isRsaKey;
            string keyText = ExtractKeyFromPemAsString(RawKey, isPrivateKey, out isRsaKey);
            RsaSecurityKey rsaKey = null;

            try
            {
                byte[] keyBytes = Convert.FromBase64String(keyText);
                // creating the RSA key 
                RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
                if (isPrivateKey)
                {
                    if (isRsaKey)
                    {   // ubuntu 20.04:
                        provider.ImportRSAPrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                    else
                    {   // debian 10:
                        provider.ImportPkcs8PrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                }
                else   // public key
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

        public static string ExtractKeyFromPemAsString(string rawKey, bool isPrivateKey, out bool isRsaKey)
        {
            string keyText = null;
            isRsaKey = true;
            Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString rawKey={rawKey}");
            try
            {
                // removing armor of PEM file (first and last line)
                List<string> lines = new List<string>(rawKey.Split('\n'));
                var firstline = lines[0];
                if (firstline.Contains("RSA"))
                {
                    isRsaKey = true;
                    // Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString: firstline={firstline}, contains rsa = true");
                }
                else
                {
                    isRsaKey = false;
                    // Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString: firstline={firstline}, contains rsa = false");
                }
                keyText = string.Join('\n', lines.GetRange(1, lines.Count - 2).ToArray());
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
