using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    public class DebugConfig
    {
        [JsonProperty("debugLevel"), JsonPropertyName("debugLevel")]
        public int DebugLevel { get; set; } = 0;
        
        [JsonProperty("extendedLogComplianceCheck"), JsonPropertyName("extendedLogComplianceCheck")]
        public bool ExtendedLogComplianceCheck { get; set; } = false;

        [JsonProperty("extendedLogReportGeneration"), JsonPropertyName("extendedLogReportGeneration")]
        public bool ExtendedLogReportGeneration { get; set; } = false;

        [JsonProperty("extendedLogScheduler"), JsonPropertyName("extendedLogScheduler")]
        public bool ExtendedLogScheduler { get; set; } = false;
    }
}
