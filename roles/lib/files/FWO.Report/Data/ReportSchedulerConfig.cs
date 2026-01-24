using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Report.Data
{
    public class ReportSchedulerConfig
    {
        [JsonProperty("report_schedule_id"), JsonPropertyName("report_schedule_id")]
        public int ReportScheduleID { get; set; } = 0;

        [JsonProperty("to_email"), JsonPropertyName("to_email")]
        public bool ToEmail { get; set; } = false;
        [JsonProperty("to_archive"), JsonPropertyName("to_archive")]
        public bool ToArchive { get; set; } = false;
        [JsonProperty("to_storage"), JsonPropertyName("to_storage")]
        public bool ToStorage { get; set; } = false;

        [JsonProperty("recipients"), JsonPropertyName("recipients")]
        public string Recipients { get; set; } = "";
        [JsonProperty("subject"), JsonPropertyName("subject")]
        public string Subject { get; set; } = "";
        [JsonProperty("body"), JsonPropertyName("body")]
        public string Body { get; set; } = "";

        public ReportSchedulerConfig()
        {
            
        }
        
        public ReportSchedulerConfig(int id)
        {
            ReportScheduleID = id;
        }



    }

}