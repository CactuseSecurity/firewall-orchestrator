using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class SuperManager
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        public SuperManager()
        {}
        
        public SuperManager(SuperManager superManager)
        {
            Id = superManager.Id;
            Name = superManager.Name;
        }
    }
}
