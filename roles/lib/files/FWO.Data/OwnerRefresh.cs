using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    /// <summary>
    /// Result of the refresh_view_rule_with_owner mutation.
    /// </summary>
    public class OwnerRefresh
    {
        [JsonProperty("status"), JsonPropertyName("status")]
        private string Status { get; set; } = "";


        public string GetStatus()
        {
            return Status;
        }
    }
}
