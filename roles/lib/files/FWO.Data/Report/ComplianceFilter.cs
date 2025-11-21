using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Report
{
    public class ComplianceFilter
    {
        [JsonProperty("diff_reference_in_days"), JsonPropertyName("diff_reference_in_days")]
        public int DiffReferenceInDays { get; set; } = 0;
        [JsonProperty("show_non_impact_rules"), JsonPropertyName("show_non_impact_rules")]
        public bool ShowNonImpactRules { get; set; } = false;

        public ComplianceFilter()
        {

        }

        public ComplianceFilter(ComplianceFilter complianceFilter)
        {
            DiffReferenceInDays = complianceFilter.DiffReferenceInDays;
            ShowNonImpactRules = complianceFilter.ShowNonImpactRules;
        }
    }

}