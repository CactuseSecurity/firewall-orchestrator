using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Report
{
    public class ComplianceFilter
    {
        [JsonProperty("diff_reference_in_days"), JsonPropertyName("diff_reference_in_days")]
        public int DiffReferenceInDays { get; set; } = 0;
        [JsonProperty("show_compliant_rules"), JsonPropertyName("show_compliant_rules")]
        public bool ShowCompliantRules { get; set; } = false;

        public ComplianceFilter()
        {

        }

        public ComplianceFilter(ComplianceFilter complianceFilter)
        {
            DiffReferenceInDays = complianceFilter.DiffReferenceInDays;
            ShowCompliantRules = complianceFilter.ShowCompliantRules;
        }
    }

}