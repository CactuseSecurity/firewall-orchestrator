using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Modelling
{
    public class RuleRecognitionOption
    {
		[JsonProperty("nwRegardIp"), JsonPropertyName("nwRegardIp")]
        public bool NwRegardIp { get; set; } = true;

		[JsonProperty("nwRegardName"), JsonPropertyName("nwRegardName")]
        public bool NwRegardName { get; set; } = false;

		[JsonProperty("nwRegardGroupName"), JsonPropertyName("nwRegardGroupName")]
        public bool NwRegardGroupName { get; set; } = false;

		[JsonProperty("nwResolveGroup"), JsonPropertyName("nwResolveGroup")]
        public bool NwResolveGroup { get; set; } = false;

		[JsonProperty("nwSeparateGroupAnalysis"), JsonPropertyName("nwSeparateGroupAnalysis")]
        public bool NwSeparateGroupAnalysis { get; set; } = true;

		[JsonProperty("svcRegardPortAndProt"), JsonPropertyName("svcRegardPortAndProt")]
        public bool SvcRegardPortAndProt { get; set; } = true;

		[JsonProperty("svcRegardName"), JsonPropertyName("svcRegardName")]
        public bool SvcRegardName { get; set; } = false;

		[JsonProperty("svcRegardGroupName"), JsonPropertyName("svcRegardGroupName")]
        public bool SvcRegardGroupName { get; set; } = false;

		[JsonProperty("svcResolveGroup"), JsonPropertyName("svcResolveGroup")]
        public bool SvcResolveGroup { get; set; } = true;

		[JsonProperty("svcSplitPortRanges"), JsonPropertyName("svcSplitPortRanges")]
        public bool SvcSplitPortRanges { get; set; } = false;
    }
}
