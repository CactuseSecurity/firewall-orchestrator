using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ActionCode
    {
        DeleteManagement,
        DeleteGateway,
        AddManagement,
        AddGatewayToNewManagement,
        AddGatewayToExistingManagement
    }

    public class ActionItem
    {
        [JsonProperty("data_issue_id"), JsonPropertyName("data_issue_id")]
        public long Id { get; set; }

        [JsonProperty("suspected_cause"), JsonPropertyName("suspected_cause")]
        public string? Supermanager { get; set; }

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? ActionType { get; set; }

        [JsonProperty("issue_mgm_id"), JsonPropertyName("issue_mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("issue_dev_id"), JsonPropertyName("issue_dev_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("json_data"), JsonPropertyName("json_data")]
        public String? JsonData { get; set; }

        public ActionItem()
        {}

        public ActionItem(LogEntry logEntry)
        {
            Id = logEntry.Id;
            Supermanager = logEntry.SuspectedCause;
            ActionType = logEntry.Description;
            ManagementId = logEntry.ManagementId;
            DeviceId = logEntry.DeviceId;
            JsonData = logEntry.JsonData;
        }
    }
}
