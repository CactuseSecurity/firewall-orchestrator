using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public abstract class FlowGroup
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        public abstract long Id { get; set; }
        public abstract string Hash { get; set; }
    }

    public class FlowNwGroup : FlowGroup
    {
        [JsonProperty("nwgroup_id"), JsonPropertyName("nwgroup_id")]
        public override long Id { get; set; }

        [JsonProperty("nwgrp_hash"), JsonPropertyName("nwgrp_hash")]
        public override string Hash { get; set; } = "";
    }

    public class FlowSvcGroup : FlowGroup
    {
        [JsonProperty("svcgroup_id"), JsonPropertyName("svcgroup_id")]
        public override long Id { get; set; }

        [JsonProperty("svcgrp_hash"), JsonPropertyName("svcgrp_hash")]
        public override string Hash { get; set; } = "";
    }
}
