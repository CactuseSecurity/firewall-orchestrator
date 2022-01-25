using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ReportTemplate
    {
        [JsonProperty("report_template_id"), JsonPropertyName("report_template_id")]
        public int Id { get; set; }

        [JsonProperty("report_template_name"), JsonPropertyName("report_template_name")]
        public string Name { get; set; } = "";

        [JsonProperty("report_template_create"), JsonPropertyName("report_template_create")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("report_template_comment"), JsonPropertyName("report_template_comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("report_template_owner"), JsonPropertyName("report_template_owner")]
        public int Owner { get; set; }

        [JsonProperty("report_filter"), JsonPropertyName("report_filter")]
        public string Filter { get; set; } = "";
    }
}
