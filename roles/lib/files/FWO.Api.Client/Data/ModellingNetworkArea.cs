using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNetworkArea : ModellingNwGroup
    {
        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<NetworkSubnetWrapper> Subnets { get; set; } = new();


        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            foreach(var subnet in Subnets)
            {
                if(subnet.Content.Sanitize())
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
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string? Ip { get; set; }

       [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string? IpEnd { get; set; }


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Ip = Sanitizer.SanitizeOpt(Ip, ref shortened);
            IpEnd = Sanitizer.SanitizeOpt(IpEnd, ref shortened);
            return shortened;
        }
    }

    public class NetworkSubnetWrapper
    {
        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public NetworkSubnet Content { get; set; } = new();

        public static NetworkSubnet[] Resolve(List<NetworkSubnetWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }

}
