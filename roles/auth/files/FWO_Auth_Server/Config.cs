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

        private Dictionary<String, String> conf;

        public Dictionary<String, String> GetConf()
        {
            return conf;
        }

        public void SetConf(Dictionary<String, String> value)
        {
            conf = value;
        }

        public Config(String FileName)
        {
            // var stream = Yaml.StreamFrom(FileName);
            String yamlString = File.ReadAllText(FileName).TrimEnd();
            var YamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            this.SetConf(YamlDeserializer.Deserialize<Dictionary<String,String>>(yamlString));
            this.SetFileName(FileName ?? throw new ArgumentNullException(nameof(FileName)));
        }
        public String GetConfigValue(String ConfigKey)
        {
            return GetConf()[ConfigKey];
        }
    }
}
