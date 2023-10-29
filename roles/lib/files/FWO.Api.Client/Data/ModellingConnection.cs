using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ConnectionField
    {
        Source = 1,
        Destination = 2
    }

    public class ModellingConnection
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

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ModellingServiceWrapper> Services { get; set; } = new();

        [JsonProperty("service_groups"), JsonPropertyName("service_groups")]
        public List<ModellingServiceGroupWrapper> ServiceGroups { get; set; } = new();

        [JsonProperty("source_app_servers"), JsonPropertyName("source_app_servers")]
        public List<ModellingAppServerWrapper> SourceAppServers { get; set; } = new();

        [JsonProperty("source_app_roles"), JsonPropertyName("source_app_roles")]
        public List<ModellingAppRoleWrapper> SourceAppRoles { get; set; } = new();

        [JsonProperty("destination_app_servers"), JsonPropertyName("destination_app_servers")]
        public List<ModellingAppServerWrapper> DestinationAppServers { get; set; } = new();

        [JsonProperty("destination_app_roles"), JsonPropertyName("destination_app_roles")]
        public List<ModellingAppRoleWrapper> DestinationAppRoles { get; set; } = new();
    }
}
