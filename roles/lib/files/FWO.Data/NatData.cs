using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
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
        public NetworkLocation[] TranslatedFroms { get; set; } = [];

        [JsonProperty("rule_dst_neg"), JsonPropertyName("rule_dst_neg")]
        public bool TranslatedDestinationNegated { get; set; }

        [JsonProperty("rule_dst"), JsonPropertyName("rule_dst")]
        public string TranslatedDestination { get; set; } = "";

        [JsonProperty("rule_tos"), JsonPropertyName("rule_tos")]
        public NetworkLocation[] TranslatedTos { get; set; } = [];

        /// <summary>
        /// Creates an empty translation.
        /// </summary>
        public NatData()
        { }

        /// <summary>
        /// Creates a copy of a translation. The objects and services themselves are shared with the original.
        /// </summary>
        /// <param name="natData">Translation to copy.</param>
        public NatData(NatData natData)
        {
            TranslatedServiceNegated = natData.TranslatedServiceNegated;
            TranslatedService = natData.TranslatedService;
            TranslatedServices = natData.TranslatedServices;
            TranslatedSourceNegated = natData.TranslatedSourceNegated;
            TranslatedSource = natData.TranslatedSource;
            TranslatedFroms = natData.TranslatedFroms;
            TranslatedDestinationNegated = natData.TranslatedDestinationNegated;
            TranslatedDestination = natData.TranslatedDestination;
            TranslatedTos = natData.TranslatedTos;
        }
    }
}
