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


        private static RsaSecurityKey? jwtPrivateKey = null;
        public static RsaSecurityKey JwtPrivateKey
        {
            get
            {
                jwtPrivateKey = CriticalConfigValueLoaded(jwtPrivateKey);
                return jwtPrivateKey;
            }
        }

        private static RsaSecurityKey? jwtPublicKey = null;
        public static RsaSecurityKey JwtPublicKey
        {
            get
            {
                jwtPublicKey = CriticalConfigValueLoaded(jwtPublicKey);
                return jwtPublicKey;
            }
        }

        private static string? apiServerUri = null;
        public static string ApiServerUri
        {
            get
            {
                apiServerUri = CriticalConfigValueLoaded(apiServerUri);
                return apiServerUri;
            }
        }

        private static string? middlewareServerNativeUri = null;
        public static string MiddlewareServerNativeUri
        {
            get
            {
                middlewareServerNativeUri = CriticalConfigValueLoaded(middlewareServerNativeUri);
                return middlewareServerNativeUri;
            }
        }

        private static string? middlewareServerUri = null;
        public static string MiddlewareServerUri
        {
            get
            {
                middlewareServerUri = CriticalConfigValueLoaded(middlewareServerUri);
                return middlewareServerUri;
            }
        }

        private static string? productVersion = null;
        public static string ProductVersion
        {
            get
            {
                productVersion = CriticalConfigValueLoaded(productVersion);
                return productVersion;
            }
        }

        private static Dictionary<string,string> customSettings = new Dictionary<string,string>();
        public Dictionary<string,string> CustomSettings
        {
            get
            {
                return customSettings;
            }
        }

        static ConfigFile()
        {
            Read(configPath, jwtPrivateKeyPath, jwtPublicKeyPath);
        }

        private static void Read(string configFilePath, string privateKeyFilePath, string publicKeyFilePath)
        {
            try
            {
                // Reset all values
                jwtPrivateKey = null;
                jwtPublicKey = null;
                middlewareServerNativeUri = null;
                middlewareServerUri = null;
                apiServerUri = null;
                productVersion = null;

                // Read config as json from file
                string configFile = System.IO.File.ReadAllText(configFilePath).TrimEnd();

                // Deserialize config to dictionary
                Dictionary<string, string> configFileData = JsonSerializer.Deserialize<Dictionary<string, string>>(configFile) ?? throw new Exception("Config file could not be parsed.");

                // Errors can be ignored. If a configuration value that could not be loaded is requested from outside this class, an excpetion is thrown. See NotNullCriticalConfigValue()

                // Try to read jwt private key
                IgnoreExceptions(() => jwtPrivateKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(privateKeyFilePath), isPrivateKey: true));

                // Try to read jwt public key
                IgnoreExceptions(() => jwtPublicKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(publicKeyFilePath), isPrivateKey: false));

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
#if RELEASE
                Environment.Exit(1); // Exit with error
#endif
                throw;
            }
        }

        private static ConfigValueType CriticalConfigValueLoaded<ConfigValueType>(ConfigValueType? configValue)
        {
            if (configValue == null)
            {
                Log.WriteError("Config value read", $"A necessary config value could not be found.", LogStackTrace: true);
#if RELEASE
                Environment.Exit(1); // Exit with error
#endif
                throw new ApplicationException("A necessary config value could not be found.");
            }
            else
            {
                return configValue;
            }
        }
        
        private static void IgnoreExceptions(Action method)
        {
            try { method(); } catch (Exception e){ Log.WriteDebug("Config value", $"Config value could not be loaded. Error: {e.Message}"); }
        }
    }
}
