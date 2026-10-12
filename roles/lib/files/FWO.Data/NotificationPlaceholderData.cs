using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    /// <summary>
    /// Transient values supplied by a workflow caller for notification placeholder replacement.
    /// </summary>
    public class NotificationPlaceholderData
    {
        [JsonProperty("requesting_app_name"), JsonPropertyName("requesting_app_name")]
        public string RequestingAppName { get; set; } = "";

        [JsonProperty("requesting_app_id"), JsonPropertyName("requesting_app_id")]
        public string RequestingAppId { get; set; } = "";

        [JsonProperty("interface_name"), JsonPropertyName("interface_name")]
        public string InterfaceName { get; set; } = "";

        [JsonProperty("interface_link_text"), JsonPropertyName("interface_link_text")]
        public string InterfaceLinkText { get; set; } = "";

        [JsonProperty("interface_link_name"), JsonPropertyName("interface_link_name")]
        public string InterfaceLinkName { get; set; } = "";

        [JsonProperty("interface_link_url"), JsonPropertyName("interface_link_url")]
        public string InterfaceLinkUrl { get; set; } = "";

        [JsonProperty("new_interface_name"), JsonPropertyName("new_interface_name")]
        public string NewInterfaceName { get; set; } = "";

        [JsonProperty("new_interface_link_text"), JsonPropertyName("new_interface_link_text")]
        public string NewInterfaceLinkText { get; set; } = "";

        [JsonProperty("new_interface_link_name"), JsonPropertyName("new_interface_link_name")]
        public string NewInterfaceLinkName { get; set; } = "";

        [JsonProperty("new_interface_link_url"), JsonPropertyName("new_interface_link_url")]
        public string NewInterfaceLinkUrl { get; set; } = "";

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string Reason { get; set; } = "";

        [JsonProperty("content"), JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonProperty("user_name"), JsonPropertyName("user_name")]
        public string UserName { get; set; } = "";

        [JsonProperty("requester_name"), JsonPropertyName("requester_name")]
        public string RequesterName { get; set; } = "";

        [JsonProperty("request_date"), JsonPropertyName("request_date")]
        public string RequestDate { get; set; } = "";
    }
}
