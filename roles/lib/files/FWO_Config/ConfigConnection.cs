using FWO.Logging;
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
        /// Path to config file
        /// </summary>
        private const string configPath = "/etc/fworch/fworch.yaml";

        /// <summary>
        /// Path to jwt public key
        /// </summary>
        private const string jwtPublicKeyPath = "/etc/fworch/secrets/jwt_public_key.pem";

        /// <summary>
        /// Path to jwt private key
        /// </summary>
        private const string jwtPrivateKeyPath = "/etc/fworch/secrets/jwt_private_key.pem";

        /// <summary>
        /// Internal connection to auth server. Used to connect with api server.
        /// </summary>
        //private readonly AuthClient authClient;

        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        //private readonly APIConnection apiConnection;


        private RsaSecurityKey jwtPrivateKey = null;
        public RsaSecurityKey JwtPrivateKey
        {
            get
            {
                CriticalConfigValueLoaded(jwtPrivateKey);
                return jwtPrivateKey;
            }
        }

        private RsaSecurityKey jwtPublicKey = null;
        public RsaSecurityKey JwtPublicKey
        {
            get
            {
                CriticalConfigValueLoaded(jwtPublicKey);
                return jwtPublicKey;
            }
        }

        private string apiServerUri = null;
        public string ApiServerUri
        {
            get
            {
                CriticalConfigValueLoaded(apiServerUri);
                return apiServerUri;
            }
        }

        private string authServerUri = null;
        public string AuthServerUri
        {
            get
            {
                CriticalConfigValueLoaded(authServerUri);
                return authServerUri;
            }
        }

        private string productVersion = null;
        public string ProductVersion
        {
            get
            {
                CriticalConfigValueLoaded(productVersion);
                return productVersion;
            }
        }

        public ConfigConnection()
        {
            #region Config File

            try
            {              
                // Read config as yaml from file
                string yamlConfig = File.ReadAllText(configPath).TrimEnd();

                // Create yaml deserializer
                IDeserializer yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                // Deserialize yaml config to dictionary
                Dictionary<string, string> configFileData = yamlDeserializer.Deserialize<Dictionary<string, string>>(yamlConfig);

                // Errors can be ignored. If requested from outside this class error is thrown. See NotNullCriticalConfigValue()

                // Try to read jwt private key
                IgnoreExceptions(() => jwtPrivateKey = KeyImporter.ExtractKeyFromPem(File.ReadAllText(jwtPrivateKeyPath), isPrivateKey: true));

                // Try to read jwt public key
                IgnoreExceptions(() => jwtPublicKey = KeyImporter.ExtractKeyFromPem(File.ReadAllText(jwtPublicKeyPath), isPrivateKey: false));

                // Try to get auth uri
                IgnoreExceptions(() => authServerUri = configFileData["auth_uri"]);

                // Try to get api uri
                IgnoreExceptions(() => apiServerUri = configFileData["api_uri"]);
                
                // Try to get productVersion
                IgnoreExceptions(() => productVersion = configFileData["product_version"]);
            }

            catch (Exception configFileReadException)
            {
                Log.WriteError("Config file read", $"Config file could not be found.", configFileReadException);
                Environment.Exit(1); // Exit with error
            }

            #endregion
        }

        private void CriticalConfigValueLoaded(object configValue)
        {
            if (configValue == null)
            {
                Log.WriteError("Config value read", $"A necessary config value could not be found.", LogStackTrace: true);
                Environment.Exit(1); // Exit with error
            }
        }
        
        private void IgnoreExceptions(Action method)
        {
            try { method(); } catch (Exception e){ Log.WriteDebug("Config value", $"Config value could not be loaded. Error: {e.Message}"); }
        }
    }
}
