using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Config.File
{
    public class ConfigFile
    {
        private const string configPathEnvVar = "FWO_CONFIG_FILE_PATH";
        private const string jwtPublicKeyPathEnvVar = "FWO_JWT_PUBLIC_KEY_PATH";
        private const string jwtPrivateKeyPathEnvVar = "FWO_JWT_PRIVATE_KEY_PATH";
        private const string kLoginMaxDirectoriesKey = "login_max_directories";
        private const string kLoginClientAttemptsPerMinuteKey = "login_client_attempts_per_minute";
        private const string kLoginUserFailuresPerMinuteKey = "login_user_failures_per_minute";
        private const string kLoginTrustedClientHostsKey = "login_trusted_client_hosts";

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

            // Suffixed with Path so they do not shadow the outer class' properties of
            // the same name (S3218). The wire format is pinned by the attributes, so
            // renaming these does not change what is read from fworch.json.
            [JsonPropertyName("tls_client_certificate")]
            public string? TlsClientCertificatePath { get; set; }

            [JsonPropertyName("tls_client_private_key")]
            public string? TlsClientPrivateKeyPath { get; set; }

            [JsonPropertyName("tls_ca_certificate")]
            public string? TlsCaCertificatePath { get; set; }

            [JsonPropertyName("internal_ca_certificate")]
            public string? InternalCaCertificatePath { get; set; }

            [JsonPropertyName("remote_addresses")]
            public string[]? RemoteAddresses { get; set; }

            [JsonPropertyName("product_version")]
            public string? ProductVersion { get; set; }

            // Suffixed with Setting for the same reason as the certificate paths above (S3218).
            // The login settings may be edited by hand: they are read as raw values, so that a value of the wrong
            // type is ignored (see ConfigValueParser) instead of making the whole file unreadable.
            [JsonPropertyName(kLoginMaxDirectoriesKey)]
            public JsonElement? LoginMaxDirectoriesSetting { get; set; }

            [JsonPropertyName(kLoginClientAttemptsPerMinuteKey)]
            public JsonElement? LoginClientAttemptsPerMinuteSetting { get; set; }

            [JsonPropertyName(kLoginUserFailuresPerMinuteKey)]
            public JsonElement? LoginUserFailuresPerMinuteSetting { get; set; }

            [JsonPropertyName(kLoginTrustedClientHostsKey)]
            public JsonElement? LoginTrustedClientHostsSetting { get; set; }

            [JsonPropertyName("fworch_home")]
            public string? CfgFwoHome { get; set; }
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

        public static string TlsClientCertificate
        {
            get
            {
                return CriticalConfigValueLoaded(Data.TlsClientCertificatePath);
            }
        }

        public static string TlsClientPrivateKey
        {
            get
            {
                return CriticalConfigValueLoaded(Data.TlsClientPrivateKeyPath);
            }
        }

        public static string TlsCaCertificate
        {
            get
            {
                return CriticalConfigValueLoaded(Data.TlsCaCertificatePath);
            }
        }

        /// <summary>
        /// Path to the public certificate of FWO's internal certificate authority.
        /// </summary>
        public static string InternalCaCertificate
        {
            get
            {
                return CriticalConfigValueLoaded(Data.InternalCaCertificatePath);
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

        public static string FwoHome
        {
            get
            {
                return CriticalConfigValueLoaded(Data.CfgFwoHome);
            }
        }

        /// <summary>
        /// Root directories from which customization import files and scripts may be read or executed.
        /// </summary>
        public static List<string> AllowedCustomizationRoots
        {
            get
            {
                string fwoHome = FwoHome.TrimEnd('/', '\\');
                return
                [
                    Path.Combine(fwoHome, "scripts", "customizing").Replace('\\', '/'),
                    Path.Combine(fwoHome, "etc").Replace('\\', '/')
                ];
            }
        }

        public static string[] RemoteAddresses
        {
            get
            {
                return CriticalConfigValueLoaded(Data.RemoteAddresses);
            }
        }

        /// <summary>
        /// Optional cap on the LDAP connections one login may fan out to; null if not configured or invalid.
        /// </summary>
        public static int? LoginMaxDirectories { get; private set; }

        /// <summary>
        /// Optional limit of credentialed login attempts per minute and client address; null if not configured or invalid.
        /// </summary>
        public static int? LoginClientAttemptsPerMinute { get; private set; }

        /// <summary>
        /// Optional limit of failed logins per minute for one user name and client address; null if not configured or invalid.
        /// </summary>
        public static int? LoginUserFailuresPerMinute { get; private set; }

        /// <summary>
        /// Optional hosts (for example the UI servers) exempt from the per-client login limit; null if not configured
        /// or invalid. A comma separated string or a string holding a JSON list is accepted as well.
        /// </summary>
        public static List<string>? LoginTrustedClientHosts { get; private set; }

        static ConfigFile()
        {
            Read(
                ResolvePath(configPathEnvVar, configPath),
                ResolvePath(jwtPrivateKeyPathEnvVar, jwtPrivateKeyPath),
                ResolvePath(jwtPublicKeyPathEnvVar, jwtPublicKeyPath));
        }

        private static void Read(string configFilePath, string privateKeyFilePath, string publicKeyFilePath)
        {
            try
            {
                // Read config as json from file
                string configFile = System.IO.File.ReadAllText(configFilePath).TrimEnd();

                // Deserialize config to dictionary
                Data = JsonSerializer.Deserialize<ConfigFileData>(configFile) ?? throw new JsonException("Config file could not be parsed.");
                ReadLoginSettings();

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

        /// <summary>
        /// Reads the optional login settings; values of the wrong type are logged and left unset.
        /// </summary>
        private static void ReadLoginSettings()
        {
            LoginMaxDirectories = ConfigValueParser.ReadOptionalInt(Data.LoginMaxDirectoriesSetting, kLoginMaxDirectoriesKey);
            LoginClientAttemptsPerMinute = ConfigValueParser.ReadOptionalInt(Data.LoginClientAttemptsPerMinuteSetting, kLoginClientAttemptsPerMinuteKey);
            LoginUserFailuresPerMinute = ConfigValueParser.ReadOptionalInt(Data.LoginUserFailuresPerMinuteSetting, kLoginUserFailuresPerMinuteKey);
            LoginTrustedClientHosts = ConfigValueParser.ReadOptionalStringList(Data.LoginTrustedClientHostsSetting, kLoginTrustedClientHostsKey);
        }

        private static ConfigValueType CriticalConfigValueLoaded<ConfigValueType>(ConfigValueType? configValue)
        {
            if (configValue == null)
            {
                Log.WriteError("Config value read", $"A necessary config value could not be found.", LogStackTrace: true);

                throw new InvalidOperationException("A necessary config value could not be found.");
            }

            return configValue;
        }

        private static string ResolvePath(string environmentVariableName, string fallbackPath)
        {
            string? configuredPath = Environment.GetEnvironmentVariable(environmentVariableName);

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            return fallbackPath;
        }

        private static void IgnoreExceptions(Action method)
        {
            try { method(); } catch (Exception e) { Log.WriteDebug("Config value", $"Config value could not be loaded. Error: {e.Message}"); }
        }
    }
}
