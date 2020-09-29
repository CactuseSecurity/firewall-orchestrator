using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FWO_Auth_Server
{
    public class Config
    {
        private string fileName;

        public string GetFileName()
        {
            return fileName;
        }

        public void SetFileName(string value)
        {
            fileName = value;
        }

        private Dictionary<string, string> conf;

        public Dictionary<string, string> GetConf()
        {
            return conf;
        }

        public void SetConf(Dictionary<string, string> value)
        {
            conf = value;
        }

        public Config(string FileName)
        {
            // var stream = Yaml.StreamFrom(FileName);
            string yamlString = File.ReadAllText(FileName).TrimEnd();
            var YamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            this.SetConf(YamlDeserializer.Deserialize<Dictionary<string, string>>(yamlString));
            this.SetFileName(FileName ?? throw new ArgumentNullException(nameof(FileName)));
        }
        public string GetConfigValue(string ConfigKey)
        {
            return GetConf()[ConfigKey];
        }
    }
}
