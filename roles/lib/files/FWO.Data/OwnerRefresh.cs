using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    /// <summary>
    /// Result of the refresh_view_rule_with_owner mutation.
    /// </summary>
    public class OwnerRefresh
    {

        [JsonProperty("id"), JsonPropertyName("id")]
        private int Id { get; set; } = 0;

        [JsonProperty("view_name"), JsonPropertyName("view_name")]
        private string ViewName { get; set; } = "";

        [JsonProperty("refreshed_at"), JsonPropertyName("refreshed_at")]
        private string RefreshedAt { get; set; } = "";

        [JsonProperty("status"), JsonPropertyName("status")]
        private string Status { get; set; } = "";


        public string GetStatus()
        {
            return Status;
        }
    }
}
