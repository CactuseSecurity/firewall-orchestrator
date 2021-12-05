using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class Rule
    {
        [JsonPropertyName("rule_id")]
        public long Id { get; set; }

        [JsonPropertyName("rule_uid")]
        public string Uid { get; set; } = "";

        [JsonPropertyName("rule_num_numeric")]
        public double OrderNumber { get; set; }

        [JsonPropertyName("rule_name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("rule_comment")]
        public string Comment { get; set; } = "";

        [JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonPropertyName("rule_services")]
        public ServiceWrapper[] Services { get; set; } = new ServiceWrapper[]{};

        [JsonPropertyName("rule_svc_neg")]
        public bool ServiceNegated { get; set; }

        [JsonPropertyName("rule_svc")]
        public string Service { get; set; } = "";

        [JsonPropertyName("rule_src_neg")]
        public bool SourceNegated { get; set; }

        [JsonPropertyName("rule_src")]
        public string Source { get; set; } = "";

        [JsonPropertyName("src_zone")]
        public NetworkZone SourceZone { get; set; } = new NetworkZone();

        [JsonPropertyName("rule_froms")]
        public NetworkLocation[] Froms { get; set; } = new NetworkLocation[]{};

        [JsonPropertyName("rule_dst_neg")]
        public bool DestinationNegated { get; set; }

        [JsonPropertyName("rule_dst")]
        public string Destination { get; set; } = "";

        [JsonPropertyName("dst_zone")]
        public NetworkZone DestinationZone { get; set; } = new NetworkZone();

        [JsonPropertyName("rule_tos")]
        public NetworkLocation[] Tos { get; set; } = new NetworkLocation[]{};

        [JsonPropertyName("rule_action")]
        public string Action { get; set; } = "";

        [JsonPropertyName("rule_track")]
        public string Track { get; set; } = "";

        [JsonPropertyName("section_header")]
        public string SectionHeader { get; set; } = "";

        [JsonPropertyName("rule_metadatum")]
        public RuleMetadata Metadata {get; set;} = new RuleMetadata();

        [JsonPropertyName("translate")]
        public NatData NatData {get; set;} = new NatData();

        public bool Certified { get; set; }
        public string DeviceName { get; set; } = "";
    }
}
