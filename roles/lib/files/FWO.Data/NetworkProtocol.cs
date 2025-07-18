using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkProtocol
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";


        public NetworkProtocol()
        { }

        public NetworkProtocol(IpProtocol i)
        {
            Id = i.Id;
            Name = i.Name;
        }
        
        public bool HasPorts()
        {
            return Id == 6 || Id == 17;
        }
    }
}
