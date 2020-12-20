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
        [JsonPropertyName("report_template_id")]
        public string Id { get; set; }

        [JsonPropertyName("report_schedule_owner")]
        public string Owner { get; set; }

        [JsonPropertyName("report_schedule_start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("report_schedule_repeat")]
        public string RepeatCount { get; set; }

        [JsonPropertyName("report_schedule_every")]
        public string RepeatInterval { get; set; }        
    }
}
