using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ReportFile
    {
        [JsonProperty("report_id"), JsonPropertyName("report_id")]
        public int Id { get; set; }

        [JsonProperty("report_name"), JsonPropertyName("report_name")]
        public string Name { get; set; } = "";

        [JsonProperty("report_start_time"), JsonPropertyName("report_start_time")]
        public DateTime GenerationDateStart { get; set; }

        [JsonProperty("report_end_time"), JsonPropertyName("report_end_time")]
        public DateTime GenerationDateEnd { get; set; }

        [JsonProperty("report_template"), JsonPropertyName("report_template")]
        public ReportTemplate Template { get; set; } = new ReportTemplate();

        [JsonProperty("report_template_id"), JsonPropertyName("report_template_id")]
        public int TemplateId { get; set; }

        [JsonProperty("uiuser"), JsonPropertyName("uiuser")]
        public UiUser Owner { get; set; } = new UiUser();

        [JsonProperty("report_owner_id"), JsonPropertyName("report_owner_id")]
        public int OwnerId { get; set; }

        [JsonProperty("report_json"), JsonPropertyName("report_json")]
        public string? Json { get; set; }

        [JsonProperty("report_pdf"), JsonPropertyName("report_pdf")]
        public string? Pdf { get; set; }

        [JsonProperty("report_html"), JsonPropertyName("report_html")]
        public string? Html { get; set; }

        [JsonProperty("report_csv"), JsonPropertyName("report_csv")]
        public string? Csv { get; set; }
    }
}
