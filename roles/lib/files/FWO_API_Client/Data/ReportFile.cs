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
        public string GenerationDateStart { get; set; }

        [JsonPropertyName("report_end_time")]
        public string GenerationDateEnd { get; set; }

        [JsonPropertyName("report_filetype")]
        public string Type { get; set; }

        [JsonPropertyName("report_template")]
        public ReportTemplate Template { get; set; }

        [JsonPropertyName("report_document")]
        public byte[] Content { get; set; }

        [JsonPropertyName("report_owner")]
        public string Owner { get; set; }
    }
}
