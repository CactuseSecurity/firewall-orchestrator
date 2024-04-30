using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.GlobalConstants;

namespace FWO.Api.Data
{
    public class ModellingNetworkArea : ModellingNwGroup
    {
        [JsonProperty("subnets"), JsonPropertyName("subnets")]
        public List<NetworkSubnetWrapper> Subnets { get; set; } = new();

        public int MemberCount = 0;
        
        // public override NetworkObject ToNetworkObjectGroup()
        // {
        //     Group<NetworkObject>[] objectGroups = NetworkSubnetWrapper.ResolveAsNetworkObjectGroup(Subnets ?? new List<NetworkSubnetWrapper>());
        //     return new()
        //     {
        //         Id = Id,
        //         Number = Number,
        //         Name = Name ?? "",
        //         Type = new NetworkObjectType(){ Name = ObjectType.Group },
        //         ObjectGroups = objectGroups,
        //         MemberNames = string.Join("|", Array.ConvertAll(objectGroups, o => o.Object?.Name))
        //     };
        // }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            foreach(var subnet in Subnets)
            {
                shortened |= subnet.Content.Sanitize();
            }
            return shortened;
        }
    }

    // public class ModellingNetworkAreaWrapper : ModellingNwGroupWrapper
    // {
    //     [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
    //     public new ModellingNetworkArea Content { get; set; } = new();

    //     public static ModellingNetworkArea[] Resolve(List<ModellingNetworkAreaWrapper> wrappedList)
    //     {
    //         return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
    //     }
    // }


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

        // public long Number;


        // public static NetworkObject ToNetworkObject(NetworkSubnet subnet)
        // {
        //     return new NetworkObject()
        //     {
        //         Id = subnet.Id,
        //         Number = subnet.Number,
        //         Name = subnet.Name,
        //         IP = subnet.Ip ?? "",
        //         IpEnd = subnet.IpEnd ?? ""
        //     };
        // }

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
        [JsonProperty("owner_network"), JsonPropertyName("owner_network")]
        public NetworkSubnet Content { get; set; } = new();

        public static NetworkSubnet[] Resolve(List<NetworkSubnetWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }

        // public static Group<NetworkObject>[] ResolveAsNetworkObjectGroup(List<NetworkSubnetWrapper> wrappedList)
        // {
        //     return Array.ConvertAll(wrappedList.ToArray(), wrapper => new Group<NetworkObject> {Id = wrapper.Content.Id, Object = NetworkSubnet.ToNetworkObject(wrapper.Content)});
        // }
    }
}
