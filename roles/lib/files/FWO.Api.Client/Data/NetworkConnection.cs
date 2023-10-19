using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NetworkConnection
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        // [JsonProperty("uid"), JsonPropertyName("uid")]
        // public string? Uid { get; set; } = "";

        // [JsonProperty("num_numeric"), JsonPropertyName("num_numeric")]
        // public double OrderNumber { get; set; }

        [JsonProperty("rule_name"), JsonPropertyName("rule_name")]
        public string? Name { get; set; } = "";

        [JsonProperty("rule_comment"), JsonPropertyName("rule_comment")]
        public string? Comment { get; set; } = "";

        [JsonProperty("is_interface"), JsonPropertyName("is_interface")]
        public bool IsInterface { get; set; } = false;

        [JsonProperty("used_interface_id"), JsonPropertyName("used_interface_id")]
        public long? UsedInterfaceId { get; set; }

        [JsonProperty("froms"), JsonPropertyName("froms")]
        public List<NetworkObject> Sources { get; set; } = new List<NetworkObject>{};

        public List<AppRole> SrcAppRoles { get; set; } = new List<AppRole>{};

        [JsonProperty("tos"), JsonPropertyName("tos")]
        public List<NetworkObject> Destinations { get; set; } = new List<NetworkObject>{};

        public List<AppRole> DstAppRoles { get; set; } = new List<AppRole>{};

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<NetworkService> Services { get; set; } = new List<NetworkService>{};

        // [JsonProperty("svc_neg"), JsonPropertyName("svc_neg")]
        // public bool ServiceNegated { get; set; }

        // [JsonProperty("svc"), JsonPropertyName("svc")]
        // public string Service { get; set; } = "";

        // [JsonProperty("src_neg"), JsonPropertyName("src_neg")]
        // public bool SourceNegated { get; set; }

        // [JsonProperty("src"), JsonPropertyName("src")]
        // public string Source { get; set; } = "";

        // [JsonProperty("src_zone"), JsonPropertyName("src_zone")]
        // public NetworkZone? SourceZone { get; set; } = new NetworkZone();

        // [JsonProperty("dst_neg"), JsonPropertyName("dst_neg")]
        // public bool DestinationNegated { get; set; }

        // [JsonProperty("dst"), JsonPropertyName("dst")]
        // public string Destination { get; set; } = "";

        // [JsonProperty("dst_zone"), JsonPropertyName("dst_zone")]
        // public NetworkZone? DestinationZone { get; set; } = new NetworkZone();

        // [JsonProperty("action"), JsonPropertyName("action")]
        // public string Action { get; set; } = "";

        // [JsonProperty("track"), JsonPropertyName("track")]
        // public string Track { get; set; } = "";

        // [JsonProperty("translate"), JsonPropertyName("translate")]
        // public NatData NatData {get; set;} = new NatData();

        // [JsonProperty("disabled"), JsonPropertyName("disabled")]
        // public bool Disabled { get; set; }

        // [JsonProperty("owner_name"), JsonPropertyName("owner_name")]
        // public string OwnerName {get; set;} = "";

        // [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        // public int? OwnerId {get; set;}
    }
}
