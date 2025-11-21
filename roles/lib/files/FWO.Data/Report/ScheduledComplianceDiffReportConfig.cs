using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Report
{
    public class ScheduledComplianceDiffReportConfig
    {
        [JsonProperty("complianceCheckScheduledDiffReportsIntervals"), JsonPropertyName("complianceCheckScheduledDiffReportsIntervals")]
        public Dictionary<int, int> ScheduledDiffReportsIntervals { get; set; } = new();
        
    }
    
}