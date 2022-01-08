using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FWO.Config.File
{
    public class ConfigFile
    {
        /// <summary>
        /// Path to config file
        /// </summary>
        private const string basePath = "/etc/fworch";
        private const string configPath = basePath + "/fworch.json";

        /// <summary>
        /// Path to jwt public key
        /// </summary>
        private const string jwtPublicKeyPath = basePath + "/secrets/jwt_public_key.pem";

        /// <summary>
        /// Path to jwt private key
        /// </summary>
        private const string jwtPrivateKeyPath = basePath + "/secrets/jwt_private_key.pem";

        /// <summary>
        /// Internal connection to middleware server. Used to connect with api server.
        /// </summary>
        //private readonly MiddlewareClient middlewareClient;

        /// <summary>
        /// Internal connection to api server. Used to get/edit config data.
        /// </summary>
        //private readonly APIConnection apiConnection;


        private RsaSecurityKey? jwtPrivateKey = null;
        public RsaSecurityKey JwtPrivateKey
        {
            get
            {
                jwtPrivateKey = CriticalConfigValueLoaded(jwtPrivateKey);
                return jwtPrivateKey;
            }
        }

        private RsaSecurityKey? jwtPublicKey = null;
        public RsaSecurityKey JwtPublicKey
        {
            get
            {
                jwtPublicKey = CriticalConfigValueLoaded(jwtPublicKey);
                return jwtPublicKey;
            }
        }

        private string? apiServerUri = null;
        public string ApiServerUri
        {
            get
            {
                apiServerUri = CriticalConfigValueLoaded(apiServerUri);
                return apiServerUri;
            }
        }

        private string? middlewareServerNativeUri = null;
        public string MiddlewareServerNativeUri
        {
            get
            {
                middlewareServerNativeUri = CriticalConfigValueLoaded(middlewareServerNativeUri);
                return middlewareServerNativeUri;
            }
        }
        private string? middlewareServerUri = null;
        public string MiddlewareServerUri
        {
            get
            {
                middlewareServerUri = CriticalConfigValueLoaded(middlewareServerUri);
                return middlewareServerUri;
            }
        }

        private string? productVersion = null;
        public string ProductVersion
        {
            get
            {
                productVersion = CriticalConfigValueLoaded(productVersion);
                return productVersion;
            }
        }

        private Dictionary<string,string> customSettings = new Dictionary<string,string>();
        public Dictionary<string,string> CustomSettings
        {
            get
            {
                return customSettings;
            }
        }

        public ConfigFile()
        {
            try
            {              
                // Read config as json from file
                string configFile = System.IO.File.ReadAllText(configPath).TrimEnd();

                // Deserialize config to dictionary
                Dictionary<string, string> configFileData = JsonSerializer.Deserialize<Dictionary<string,string>>(configFile) ?? throw new Exception("Config file could not be parsed.");

                // Errors can be ignored. If a configuration value that could not be loaded is requested from outside this class, an excpetion is thrown. See NotNullCriticalConfigValue()

                // Try to read jwt private key
                IgnoreExceptions(() => jwtPrivateKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(jwtPrivateKeyPath), isPrivateKey: true));

                // Try to read jwt public key
                IgnoreExceptions(() => jwtPublicKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(jwtPublicKeyPath), isPrivateKey: false));

                // Try to get uri of the middleware server (http)
                IgnoreExceptions(() => middlewareServerNativeUri = configFileData["middleware_native_uri"]);

                // Try to get uri of the middleware server reverse proxy (https)
                IgnoreExceptions(() => middlewareServerUri = configFileData["middleware_uri"]);

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
        }

        public Dictionary<string,string> ReadAdditionalConfigFile(string relativePath, List<string> keys)
        {
            try{
                string configFileContent = System.IO.File.ReadAllText(basePath + "/" + relativePath);
                Dictionary<string, string> configFileData = new Dictionary<string, string>();
                configFileData = JsonSerializer.Deserialize<Dictionary<string,string>>(configFileContent) ?? throw new Exception("Config file could not be parsed.");
                customSettings = configFileData;
                // foreach (string key in keys)
                //     customSettings.Add(key, configFileData[key]);
            }
            catch (Exception configFileReadException)
            {
                Log.WriteError("Config file read", $"Config file '{basePath + relativePath}' could not be read", configFileReadException);
            }
            return customSettings;
        }

        public bool ConfigFileCreate(string relativePath, string fileContent = "")
        {
            try{
                System.IO.File.WriteAllText(basePath + relativePath, fileContent);
            }
            catch (Exception configFileWriteException)
            {
                Log.WriteError("Config file write", $"Config file '{basePath + relativePath}' could not be written", configFileWriteException);
                return false;
            }
            return true;
        }

        private ConfigValueType CriticalConfigValueLoaded<ConfigValueType>(ConfigValueType? configValue)
        {
            if (configValue == null)
            {
                Log.WriteError("Config value read", $"A necessary config value could not be found.", LogStackTrace: true);
                Environment.Exit(1); // Exit with error
                throw new ApplicationException("unreachable");
            }
            else
            {
                return configValue;
            }
        }
        
        private void IgnoreExceptions(Action method)
        {
            try { method(); } catch (Exception e){ Log.WriteDebug("Config value", $"Config value could not be loaded. Error: {e.Message}"); }
        }
    }
}
