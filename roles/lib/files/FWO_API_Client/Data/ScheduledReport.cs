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

        [JsonPropertyName("report_schedule_owner_user")]
        public UiUser Owner { get; set; }

        [JsonPropertyName("report_schedule_start_time")]
        public DateTime StartTime { get; set; } = DateTime.Now;

        [JsonPropertyName("report_schedule_repeat")]
        public int RepeatOffset { get; set; } = 1;

        [JsonPropertyName("report_schedule_every")]
        public Interval RepeatInterval { get; set; }

        [JsonPropertyName("report_schedule_template")]
        public ReportTemplate Template { get; set; } = new ReportTemplate();

        [JsonPropertyName("report_schedule_formats")]
        public List<FileFormat> OutputFormat { get; set; } = new List<FileFormat>();

        [JsonPropertyName("report_schedule_active")]
        public bool Active { get; set; }
    }

    public enum Interval
    {
        Days = 2,
        Weeks = 3,
        Months = 4,
        Years = 5
    }
}
