using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Api.Data
{
    public struct ExternalVarKeys
    {
        public const string BundledTasks = "BundledTasks";
    }

    public class ExternalRequest
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long TicketId { get; set; }

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("ext_ticket_system"), JsonPropertyName("ext_ticket_system")]
        public string ExtTicketSystem { get; set; } = "";

        [JsonProperty("ext_request_type"), JsonPropertyName("ext_request_type")]
        public string ExtRequestType { get; set; } = "";

        [JsonProperty("ext_request_content"), JsonPropertyName("ext_request_content")]
        public string ExtRequestContent { get; set; } = "";

        [JsonProperty("ext_query_variables"), JsonPropertyName("ext_query_variables")]
        public string ExtQueryVariables { get; set; } = "";

        [JsonProperty("ext_request_state"), JsonPropertyName("ext_request_state")]
        public string ExtRequestState { get; set; } = "";

        [JsonProperty("ext_ticket_id"), JsonPropertyName("ext_ticket_id")]
        public string? ExtTicketId { get; set; }

        public string? LastMessage { get; set; }
    }
}
