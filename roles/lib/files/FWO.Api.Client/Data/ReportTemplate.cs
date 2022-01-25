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
        
        [JsonProperty("report_parameters"), JsonPropertyName("report_parameters")]
        public ReportParams ReportParams { get; set; } = new ReportParams();

   
        public ReportTemplate()
        {}

        public ReportTemplate(string filter, DeviceFilter deviceFilter, int? reportType, TimeFilter timeFilter)
        {
            Filter = filter;
            ReportParams.DeviceFilter = deviceFilter;
            ReportParams.ReportType = reportType;
            ReportParams.TimeFilter = timeFilter;
        }

        public void Sanitize()
        {
            Name = Sanitizer.SanitizeMand(Name);
            Comment = Sanitizer.SanitizeMand(Comment);
        }
    }

    public class ReportParams
    {
        [JsonProperty("report_type"), JsonPropertyName("report_type")]
        public int? ReportType { get; set; } = 0;
        
        [JsonProperty("device_filter"), JsonPropertyName("device_filter")]
        public DeviceFilter DeviceFilter { get; set; } = new DeviceFilter();

        [JsonProperty("time_filter"), JsonPropertyName("time_filter")]
        public TimeFilter TimeFilter { get; set; } = new TimeFilter();
    }
}
