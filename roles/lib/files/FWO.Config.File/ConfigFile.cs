using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// All config data found in the main config file
        /// </summary>
        private class ConfigFileData
        {
            /// <summary>
            /// Uri of the middleware server (http)
            /// </summary>
            [JsonPropertyName("middleware_native_uri")]
            public string? MiddlewareServerNativeUri { get; set; }

            /// <summary>
            /// Uri of the middleware server reverse proxy (https)
            /// </summary>
            [JsonPropertyName("middleware_uri")]
            public string? MiddlewareServerUri { get; set; }

            [JsonPropertyName("api_uri")]
            public string? ApiServerUri { get; set; }

            [JsonPropertyName("remote_addresses")]
            public string[]? RemoteAddresses { get; set; }

            [JsonPropertyName("product_version")]
            public string? ProductVersion { get; set; }
        }

        /// <summary>
        /// Config file data found in the main config file
        /// </summary>
        private static ConfigFileData Data { get; set; } = new ConfigFileData();

        private static RsaSecurityKey? jwtPrivateKey = null;
        public static RsaSecurityKey JwtPrivateKey
        {
            get
            {
                return CriticalConfigValueLoaded(jwtPrivateKey);
            }
        }

        private static RsaSecurityKey? jwtPublicKey = null;
        public static RsaSecurityKey JwtPublicKey
        {
            get
            {
                return CriticalConfigValueLoaded(jwtPublicKey);
            }
        }

        public static string ApiServerUri
        {
            get
            {
                return CriticalConfigValueLoaded(Data.ApiServerUri);
            }
        }

        public static string MiddlewareServerNativeUri
        {
            get
            {
                return CriticalConfigValueLoaded(Data.MiddlewareServerNativeUri);
            }
        }

        public static string MiddlewareServerUri
        {
            get
            {
                return CriticalConfigValueLoaded(Data.MiddlewareServerUri);
            }
        }

        public static string ProductVersion
        {
            get
            {
                return CriticalConfigValueLoaded(Data.ProductVersion);
            }
        }

        public static string[] RemoteAddresses
        {
            get
            {
                return CriticalConfigValueLoaded(Data.RemoteAddresses);
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
                // Read config as json from file
                string configFile = System.IO.File.ReadAllText(configFilePath).TrimEnd();

                // Deserialize config to dictionary
                Data = JsonSerializer.Deserialize<ConfigFileData>(configFile) ?? throw new Exception("Config file could not be parsed.");

                // Errors can be ignored. If a configuration value that could not be loaded is requested from outside this class, an excpetion is thrown. See CriticalConfigValueLoaded()

                // Reset all keys
                jwtPrivateKey = null;
                jwtPublicKey = null;

                // Try to read jwt private key
                IgnoreExceptions(() => jwtPrivateKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(privateKeyFilePath), isPrivateKey: true));

                // Try to read jwt public key
                IgnoreExceptions(() => jwtPublicKey = KeyImporter.ExtractKeyFromPem(System.IO.File.ReadAllText(publicKeyFilePath), isPrivateKey: false));

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
            try { method(); } catch (Exception e) { Log.WriteDebug("Config value", $"Config value could not be loaded. Error: {e.Message}"); }
        }
    }
}
