using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ReportTemplate
    {
        [JsonPropertyName("report_template_id")]
        public int Id { get; set; }

        [JsonPropertyName("report_template_name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("report_template_create")]
        public DateTime CreationDate { get; set; }

        [JsonPropertyName("report_template_comment")]
        public string Comment { get; set; } = "";

        [JsonPropertyName("report_template_owner")]
        public int Owner { get; set; }

        [JsonPropertyName("report_filter")]
        public string Filter { get; set; } = "";
    }
}
