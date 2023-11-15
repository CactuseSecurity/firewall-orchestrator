using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{


    public class ModellingNetworkArea : ModellingNwGroupObject
    {
        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<NetworkSubnet> Subnets { get; set; } = new();


        public override string Display()
        {
            return (IsDeleted ? "*" : "") + Name; // + " (" + IdString + ")"
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"oi oi-folder\"></span> " + Display();
        }


        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            foreach(var subnet in Subnets)
            {
                if(subnet.Sanitize())
                {
                    shortened = true;
                }
            }
            return shortened;
        }
    }

    public class NetworkSubnet
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // -> cidr
        [JsonProperty("network"), JsonPropertyName("network")]
        public string? Network { get; set; }


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Network = Sanitizer.SanitizeOpt(Network, ref shortened);
            return shortened;
        }
    }
}
