using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class Rule
    {
        [JsonPropertyName("rule_id")]
        public int Id { get; set; }

        [JsonPropertyName("rule_uid")]
        public string Uid { get; set; }

        [JsonPropertyName("rule_num_numeric")]
        public double OrderNumber { get; set; }

        [JsonPropertyName("rule_name")]
        public string Name { get; set; }

        [JsonPropertyName("rule_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonPropertyName("rule_services")]
        public ServiceWrapper[] Services { get; set; }

        [JsonPropertyName("rule_svc_neg")]
        public bool ServiceNegated { get; set; }

        [JsonPropertyName("rule_svc")]
        public string Service { get; set; }

        [JsonPropertyName("rule_src_neg")]
        public bool SourceNegated { get; set; }

        [JsonPropertyName("rule_src")]
        public string Source { get; set; }

        [JsonPropertyName("src_zone")]
        public NetworkZone SourceZone { get; set; }

        [JsonPropertyName("rule_froms")]
        public NetworkLocation[] Froms { get; set; }

        [JsonPropertyName("rule_dst_neg")]
        public bool DestinationNegated { get; set; }

        [JsonPropertyName("rule_dst")]
        public string Destination { get; set; }

        [JsonPropertyName("dst_zone")]
        public NetworkZone DestinationZone { get; set; }

        [JsonPropertyName("rule_tos")]
        public NetworkLocation[] Tos { get; set; }

        [JsonPropertyName("rule_action")]
        public string Action { get; set; }

        [JsonPropertyName("rule_track")]
        public string Track { get; set; }

        [JsonPropertyName("section_header")]
        public string SectionHeader { get; set; }

        [JsonPropertyName("rule_metadatum")]
        public RuleMetadata Metadata {get; set;}

        public bool Certified { get; set; }
        public string DeviceName { get; set; }
    }
}
