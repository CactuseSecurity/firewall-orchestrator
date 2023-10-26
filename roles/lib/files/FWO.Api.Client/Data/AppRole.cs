using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using NetTools;

namespace FWO.Api.Data
{
    public class AppRole
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
        public string NameFixedPart 
        { 
            get 
            { 
                return Name.Length >= FixedPartLength ? Name.Substring(0, FixedPartLength) : Name;
            }
            set 
            { 
                if(Name.Length >= FixedPartLength)
                {
                    Name = Name.Substring(0, FixedPartLength) + value;
                }
                else
                {
                    Name = value;
                }
            }
        }
        public string NameFreePart
        {
            get
            { 
                return Name.Length >= FixedPartLength ? Name.Substring(FixedPartLength - 1) : "";
            }
            set 
            {
                if(Name.Length >= FixedPartLength)
                {
                    Name = Name.Substring(0, FixedPartLength - 1) + value;
                }
                else
                {
                    for (int i = 0; i < FixedPartLength - Name.Length; i++)
                    {
                        Name += " ";
                    }
                    Name += value;
                }
            }
        }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("appServers"), JsonPropertyName("appServers")]
        public List<NetworkObject> NetworkObjects { get; set; } = new List<NetworkObject>{};

        public NetworkArea Area { get; set; }

        public static int FixedPartLength = 4;

    }
}
