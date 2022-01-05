using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class DeviceType
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("version"), JsonPropertyName("version")]
        public string Version { get; set; } = "";

        // [JsonProperty("predefinedObjects"), JsonPropertyName("predefinedObjects")]
        // public ??? PredefinedObjects { get; set; }

        public DeviceType()
        {}
        
        public DeviceType(DeviceType deviceType)
        {
            Id = deviceType.Id;
            Name = deviceType.Name;
            Version = deviceType.Version;
        }

        public string NameVersion()
        {
            return Name + " " + Version;
        }
    }
}
