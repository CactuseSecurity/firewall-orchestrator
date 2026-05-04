using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowSvcGroup
    {
        [JsonProperty("svcgroup_id"), JsonPropertyName("svcgroup_id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("svcgrp_hash"), JsonPropertyName("svcgrp_hash")]
        public string Hash { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }
    }
}