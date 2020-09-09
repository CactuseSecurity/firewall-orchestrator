using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FWO.Config
{
    public class Config
    {
        private readonly string JwtPrivateKeyPath;

        public Config(string JwtPrivateKeyPath, string InspectorPassword)
        {

        }

        public Dictionary<string, string> Data { get; set; }

        public string this[string ConfigKey]
        {
            get { return Data[ConfigKey]; }
            set { Data[ConfigKey] = value; }
        }

        public Config(string FileName)
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
