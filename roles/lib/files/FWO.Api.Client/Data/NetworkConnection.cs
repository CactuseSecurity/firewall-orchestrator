using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ConnectionField
    {
        Source = 1,
        Destination = 2
    }

    public class NetworkConnection
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; } = "";

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; } = "";

        [JsonProperty("is_interface"), JsonPropertyName("is_interface")]
        public bool IsInterface { get; set; } = false;

        [JsonProperty("used_interface_id"), JsonPropertyName("used_interface_id")]
        public long? UsedInterfaceId { get; set; }


        public List<NetworkObject> Sources { get; set; } = new List<NetworkObject>{};
        public List<AppRole> SrcAppRoles { get; set; } = new List<AppRole>{};

        public List<NetworkObject> Destinations { get; set; } = new List<NetworkObject>{};
        public List<AppRole> DstAppRoles { get; set; } = new List<AppRole>{};

        public List<ModellingService> Services { get; set; } = new List<ModellingService>{};
        public List<ServiceGroup> ServiceGroups { get; set; } = new List<ServiceGroup>{};

    }
}
