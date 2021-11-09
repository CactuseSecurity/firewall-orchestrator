using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class NatData
    {
        [JsonPropertyName("rule_svc_neg")]
        public bool TranslatedServiceNegated { get; set; }

        [JsonPropertyName("rule_svc")]
        public string TranslatedService { get; set; }

        [JsonPropertyName("rule_services")]
        public ServiceWrapper[] TranslatedServices { get; set; }


        [JsonPropertyName("rule_src_neg")]
        public bool TranslatedSourceNegated { get; set; }

        [JsonPropertyName("rule_src")]
        public string TranslatedSource { get; set; }

        [JsonPropertyName("rule_froms")]
        public NetworkLocation[] TranslatedFroms { get; set; }


        [JsonPropertyName("rule_dst_neg")]
        public bool TranslatedDestinationNegated { get; set; }

        [JsonPropertyName("rule_dst")]
        public string TranslatedDestination { get; set; }

        [JsonPropertyName("rule_tos")]
        public NetworkLocation[] TranslatedTos { get; set; }
    }
}
