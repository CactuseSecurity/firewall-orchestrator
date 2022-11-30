using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class Rule
    {
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long Id { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? Uid { get; set; } = "";

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmtId { get; set; }

        [JsonProperty("rule_num_numeric"), JsonPropertyName("rule_num_numeric")]
        public double OrderNumber { get; set; }

        public int DisplayOrderNumber { get; set; }

        [JsonProperty("rule_name"), JsonPropertyName("rule_name")]
        public string? Name { get; set; } = "";

        [JsonProperty("rule_comment"), JsonPropertyName("rule_comment")]
        public string? Comment { get; set; } = "";

        [JsonProperty("rule_disabled"), JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonProperty("rule_services"), JsonPropertyName("rule_services")]
        public ServiceWrapper[] Services { get; set; } = new ServiceWrapper[]{};

        [JsonProperty("rule_svc_neg"), JsonPropertyName("rule_svc_neg")]
        public bool ServiceNegated { get; set; }

        [JsonProperty("rule_svc"), JsonPropertyName("rule_svc")]
        public string Service { get; set; } = "";

        [JsonProperty("rule_src_neg"), JsonPropertyName("rule_src_neg")]
        public bool SourceNegated { get; set; }

        [JsonProperty("rule_src"), JsonPropertyName("rule_src")]
        public string Source { get; set; } = "";

        [JsonProperty("src_zone"), JsonPropertyName("src_zone")]
        public NetworkZone? SourceZone { get; set; } = new NetworkZone();

        [JsonProperty("rule_froms"), JsonPropertyName("rule_froms")]
        public NetworkLocation[] Froms { get; set; } = new NetworkLocation[]{};
      
        [JsonProperty("rule_dst_neg"), JsonPropertyName("rule_dst_neg")]
        public bool DestinationNegated { get; set; }

        [JsonProperty("rule_dst"), JsonPropertyName("rule_dst")]
        public string Destination { get; set; } = "";

        [JsonProperty("dst_zone"), JsonPropertyName("dst_zone")]
        public NetworkZone? DestinationZone { get; set; } = new NetworkZone();

        [JsonProperty("rule_tos"), JsonPropertyName("rule_tos")]
        public NetworkLocation[] Tos { get; set; } = new NetworkLocation[]{};

        [JsonProperty("rule_action"), JsonPropertyName("rule_action")]
        public string Action { get; set; } = "";

        [JsonProperty("rule_track"), JsonPropertyName("rule_track")]
        public string Track { get; set; } = "";

        [JsonProperty("section_header"), JsonPropertyName("section_header")]
        public string? SectionHeader { get; set; } = "";

        [JsonProperty("rule_metadatum"), JsonPropertyName("rule_metadatum")]
        public RuleMetadata Metadata {get; set;} = new RuleMetadata();

        [JsonProperty("translate"), JsonPropertyName("translate")]
        public NatData NatData {get; set;} = new NatData();

        public bool Certified { get; set; }
        public string DeviceName { get; set; } = "";

        [JsonProperty("owner_name"), JsonPropertyName("owner_name")]
        public string OwnerName {get; set;} = "";

        [JsonProperty("matches"), JsonPropertyName("matches")]
        public string IpMatch {get; set;} = "";
        // public IpMatchHelper IpMatch {get; set;} = new IpMatchHelper();

    }

    // public class IpMatchHelper
    // {
    //     [JsonProperty("matches"), JsonPropertyName("matches")]
    //     public List<string> IpMatches { get; set; } = new List<string>();

    //     public override string ToString()
    //     {
    //         string result = "";
    //         foreach (string match in IpMatches)
    //         {
    //             result += match;
    //         }
    //         return result;
    //     }
    // }

}
