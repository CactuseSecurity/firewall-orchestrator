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
        // Paths to config files
        private readonly string JwtPrivateKeyPath;
        private readonly string LdapInspectorPasswordPath;

        private Dictionary<string, string> Data { get; set; }

        public string this[string ConfigKey]
        {
            get 
            { 
                return Data[ConfigKey];
            }

            set 
            { 
                Data[ConfigKey] = value;
            }
        }

        public ConfigConnection()
        {
            Task.Run(() =>
            {
                
            });
        }

        public ConfigConnection(string FileName)
        {
            // var stream = Yaml.StreamFrom(FileName);
            string yamlString = File.ReadAllText(FileName).TrimEnd();
            IDeserializer YamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            Data = YamlDeserializer.Deserialize<Dictionary<string, string>>(yamlString);           
        }
    }
}
