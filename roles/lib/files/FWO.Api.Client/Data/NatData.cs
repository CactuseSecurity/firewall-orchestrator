using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NatData
    {
        [JsonProperty("rule_svc_neg"), JsonPropertyName("rule_svc_neg")]
        public bool TranslatedServiceNegated { get; set; }

        [JsonProperty("rule_svc"), JsonPropertyName("rule_svc")]
        public string TranslatedService { get; set; } = "";

        [JsonProperty("rule_services"), JsonPropertyName("rule_services")]
        public ServiceWrapper[] TranslatedServices { get; set; } = [];

        [JsonProperty("rule_src_neg"), JsonPropertyName("rule_src_neg")]
        public bool TranslatedSourceNegated { get; set; }

        [JsonProperty("rule_src"), JsonPropertyName("rule_src")]
        public string TranslatedSource { get; set; } = "";

        [JsonProperty("rule_froms"), JsonPropertyName("rule_froms")]
        public NetworkLocation[] TranslatedFroms { get; set; } = new NetworkLocation[]{};

        [JsonProperty("rule_dst_neg"), JsonPropertyName("rule_dst_neg")]
        public bool TranslatedDestinationNegated { get; set; }

        [JsonProperty("rule_dst"), JsonPropertyName("rule_dst")]
        public string TranslatedDestination { get; set; } = "";

        [JsonProperty("rule_tos"), JsonPropertyName("rule_tos")]
        public NetworkLocation[] TranslatedTos { get; set; } = new NetworkLocation[]{};
    }
}
