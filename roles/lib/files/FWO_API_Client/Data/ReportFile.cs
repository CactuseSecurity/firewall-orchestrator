using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class ReportFile
    {
        [JsonPropertyName("report_id")]
        public int Id { get; set; }

        [JsonPropertyName("report_name")]
        public string Name { get; set; }

        [JsonPropertyName("report_start_time")]
        public DateTime GenerationDateStart { get; set; }

        [JsonPropertyName("report_end_time")]
        public DateTime GenerationDateEnd { get; set; }

        [JsonPropertyName("report_template")]
        public ReportTemplate Template { get; set; }

        [JsonPropertyName("report_template_id")]
        public int TemplateId { get; set; }

        [JsonPropertyName("uiuser")]
        public UiUser Owner { get; set; }

        [JsonPropertyName("report_owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("report_json")]
        public string Json { get; set; }

        [JsonPropertyName("report_pdf")]
        public byte[] Pdf { get; set; }

        [JsonPropertyName("report_html")]
        public string Html { get; set; }

        [JsonPropertyName("report_csv")]
        public string Csv { get; set; }
    }
}
