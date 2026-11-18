using System.Text.Json.Serialization;
using Newtonsoft.Json;


namespace FWO.Data
{
    public struct ExternalVarKeys
    {
        public const string BundledTasks = "BundledTasks";
        public const string ManagementId = "ManagementId";
        /// <summary>Scope a person released by a button: the request task number, 0 for the whole ticket.</summary>
        public const string ButtonRelease = "ButtonRelease";
        /// <summary>Marks a firewall write of a single implementation task started by its own action: nothing follows it.</summary>
        public const string SingleImplTask = "SingleImplTask";
        /// <summary>Gateways a new rule is installed on, written for all tasks of its group: their gateway ids.</summary>
        public const string InstallOnGateways = "InstallOnGateways";
        /// <summary>Implementation tasks of the same request task whose new rule the write covers: their task numbers.</summary>
        public const string CoveredTasks = "CoveredTasks";
        /// <summary>Marks the request of a task covered by the rule of another task: the task number of that task.</summary>
        public const string CoveredBy = "CoveredBy";
    }

    public class ExternalRequest
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        private FwoOwner owner = new();

        /// <summary>
        /// Owner of the request. Requests of tickets without owner, e.g. object tasks written to the firewall, have none
        /// in the database; they get an empty owner, so that displays and filters need no null check.
        /// </summary>
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner
        {
            get => owner;
            set => owner = value ?? new();
        }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long TicketId { get; set; }

        [JsonProperty("task_number"), JsonPropertyName("task_number")]
        public int TaskNumber { get; set; }

        [JsonProperty("wait_cycles"), JsonPropertyName("wait_cycles")]
        public int WaitCycles { get; set; }

        [JsonProperty("attempts"), JsonPropertyName("attempts")]
        public int Attempts { get; set; } = 0;

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

        [JsonProperty("last_creation_response"), JsonPropertyName("last_creation_response")]
        public string? LastCreationResponse { get; set; }

        [JsonProperty("last_processing_response"), JsonPropertyName("last_processing_response")]
        public string? LastProcessingResponse { get; set; }

        [JsonProperty("create_date"), JsonPropertyName("create_date")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("locked"), JsonPropertyName("locked")]
        public bool Locked { get; set; } = false;

        /// <summary>
        /// Implementation task written directly to the firewall by this request; null for a request based on request
        /// tasks, e.g. handed to Tufin SecureChange. Set when the request is created, so the way of a waiting or
        /// retried request does not change.
        /// </summary>
        [JsonProperty("impltask_id"), JsonPropertyName("impltask_id")]
        public long? ImplTaskId { get; set; }

        public string? LastMessage { get; set; }
    }

    public class ExternalRequestDataHelper
    {
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<ExternalRequest> ExternalRequests { get; set; } = [];
    }

}
