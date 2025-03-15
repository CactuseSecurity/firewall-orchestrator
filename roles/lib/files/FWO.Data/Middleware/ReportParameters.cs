using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Middleware
{
    public class ReportGetParameters
    {
        // {
        //     "device-filter": {
        //         "management-ids": [2, 4],
        //         "gateway-ids": [6,8, 9]
        //     },
        //     "report-type": "rules [default] |changes|....",
        //     "report-view": ["standard|resolved|technical|with-zones|with-labels|..."],
        //     "rule-filter": {
        //         "source-ip": ["1.2.3.0/24", "3.4.5.6"],
        //         "destination-ip": ["1.2.3.0/24", "3.4.5.6"],
        //         "ip": ["1.2.3.0/24", "3.4.5.6"],
        //         "service": [
        //         {
        //             "protocol": 17,
        //             "name": "http",
        //             "port": 4711
        //         }
        //     ],
        //     "action": "allow",
        //     "active":  true
        //     }
        // }

        [JsonProperty("device-filter"), JsonPropertyName("device-filter")]
        public ApiDeviceFilter ApiDeviceFilter { get; set; } = new();

        [JsonProperty("report-type"), JsonPropertyName("report-type")]
        public string ApiReportType { get; set; } = "";

        [JsonProperty("report-view"), JsonPropertyName("report-view")]
        public List<string> ApiReportView { get; set; } = [];

        [JsonProperty("rule-filter"), JsonPropertyName("rule-filter")]
        public ApiRuleFilter ApiRuleFilter { get; set; } = new();

        [JsonProperty("action"), JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool? Active { get; set; }
    }

    public class ApiDeviceFilter
    {
        [JsonProperty("management-ids"), JsonPropertyName("management-ids")]
        public List<int> ManagementIds { get; set; } = [];
        
        [JsonProperty("gateway-ids"), JsonPropertyName("gateway-ids")]
        public List<int> DeviceIds { get; set; } = [];
    }

    public class ApiRuleFilter
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public List<string> Ips { get; set; } = [];

        [JsonProperty("source-ip"), JsonPropertyName("source-ip")]
        public List<string> SourceIps { get; set; } = [];
        
        [JsonProperty("destination-ip"), JsonPropertyName("destination-ip")]
        public List<string> DestinationIps { get; set; } = [];

        [JsonProperty("service"), JsonPropertyName("service")]
        public List<ApiService> Services { get; set; } = [];
    }

    public class ApiService
    {
        [JsonProperty("protocol"), JsonPropertyName("protocol")]
        public int? Protocol { get; set; }
        
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("port"), JsonPropertyName("port")]
        public int? Port { get; set; }
    }
}
