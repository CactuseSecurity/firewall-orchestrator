using System;
using System.Collections.Generic;
using System.IO;
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
        private const string ConfigPath = ""; //TODO: Add path to config (absolute)

        private readonly string JwtPrivateKeyPath;

        private Dictionary<string, string> ConfigData { get; set; }

        public string this[string ConfigKey]
        {
            get
            {
                return ConfigData[ConfigKey];
            }

            set
            {
                ConfigData[ConfigKey] = value;
            }
        }

        public ConfigConnection()
        {
            // Read config as yaml from file
            string yamlConfig = File.ReadAllText(ConfigPath).TrimEnd();

            // Create yaml deserializer
            IDeserializer YamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            // Deserialize yaml config to dictionary
            ConfigData = YamlDeserializer.Deserialize<Dictionary<string, string>>(yamlConfig);
        }
    }
}
