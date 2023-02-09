using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.Api.Data;

namespace FWO.Config.Api.Data
{
    public class RecertCheckParams
    {
        [JsonProperty("check_interval"), JsonPropertyName("check_interval")]
        public Interval RecertCheckInterval { get; set; } = Interval.Months;

        [JsonProperty("check_offset"), JsonPropertyName("check_offset")]
        public int RecertCheckOffset { get; set; } = 1;

        [JsonProperty("check_weekday"), JsonPropertyName("check_weekday")]
        public int? RecertCheckWeekday { get; set; }

        [JsonProperty("check_dayofmonth"), JsonPropertyName("check_dayofmonth")]
        public int? RecertCheckDayOfMonth { get; set; }
    }
}
