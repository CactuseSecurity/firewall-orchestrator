using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowAccess
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long Id { get; set; }

        [JsonProperty("access_hash"), JsonPropertyName("access_hash")]
        public string Hash { get; set; } = "";

        [JsonProperty("requester_id"), JsonPropertyName("requester_id")]
        public int? RequesterId { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested"; // requested / denied / implemented / removed

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("access_sources"), JsonPropertyName("access_sources")]
        public List<FlowAccessSource>? Sources { get; set; }

        [JsonProperty("access_destinations"), JsonPropertyName("access_destinations")]
        public List<FlowAccessDestination>? Destinations { get; set; }

        [JsonProperty("access_services"), JsonPropertyName("access_services")]
        public List<FlowAccessService>? Services { get; set; }

        [JsonProperty("access_source_grps"), JsonPropertyName("access_source_grps")]
        public List<FlowAccessSourceGroup>? SourceGroups { get; set; }

        [JsonProperty("access_destination_grps"), JsonPropertyName("access_destination_grps")]
        public List<FlowAccessDestinationGroup>? DestinationGroups { get; set; }

        [JsonProperty("access_service_grps"), JsonPropertyName("access_service_grps")]
        public List<FlowAccessServiceGroup>? ServiceGroups { get; set; }

        [JsonProperty("access_timeobjects"), JsonPropertyName("access_timeobjects")]
        public List<FlowAccessTimeObject>? TimeObjects { get; set; }

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public List<Rule>? Rules { get; set; }

        public void GenerateAccessHash()
        {
            var sourceHashes = Sources?.Select(s => s.NwObject?.Hash ?? "") ?? new List<string>();
            var destinationHashes = Destinations?.Select(d => d.NwObject?.Hash ?? "") ?? new List<string>();
            var serviceHashes = Services?.Select(s => s.SvcObject?.Hash ?? "") ?? new List<string>();

            Hash = FlowHashGenerator.GenerateAccessHash(sourceHashes, destinationHashes, serviceHashes);
        }
    }
}
