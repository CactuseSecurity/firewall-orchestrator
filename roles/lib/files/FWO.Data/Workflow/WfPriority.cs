﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Data.Workflow
{
    public class WfPriority
    {
        [JsonProperty("numeric_prio"), JsonPropertyName("numeric_prio")]
        public int NumPrio { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("ticket_deadline"), JsonPropertyName("ticket_deadline")]
        public int TicketDeadline { get; set; }

        [JsonProperty("approval_deadline"), JsonPropertyName("approval_deadline")]
        public int ApprovalDeadline { get; set; }
    }
}
