using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public class ScheduledReport
    {
        [JsonPropertyName("report_schedule_id")]
        public int Id { get; set; }

        [JsonPropertyName("report_schedule_name")]
        public string Name { get; set; }

        [JsonPropertyName("report_schedule_owner")]
        public string Owner { get; set; }

        [JsonPropertyName("report_schedule_start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("report_schedule_repeat")]
        public int RepeatCount { get; set; }

        [JsonPropertyName("report_schedule_every")]
        public string RepeatInterval { get; set; }

        [JsonPropertyName("report_schedule_template")]
        public ReportTemplate Template { get; set; }

        [JsonPropertyName("report_schedule_active")]
        public bool Active { get; set; }

        [JsonPropertyName("report_schedule_output_format")]
        public string OutputFormat { get; set; }
    }
}
