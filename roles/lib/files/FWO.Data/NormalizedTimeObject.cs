
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedTimeObject
    {
        [JsonProperty("time_obj_uid"), JsonPropertyName("time_obj_uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("time_obj_name"), JsonPropertyName("time_obj_name")]
        public string Name { get; set; } = "";

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("end_time"), JsonPropertyName("end_time")]
        public string? EndTime { get; set; }
        public static NormalizedTimeObject FromTimeObject(TimeObject timeObject)
        {
            return new NormalizedTimeObject
            {
                Uid = timeObject.Uid,
                Name = timeObject.Name,
                StartTime = timeObject.StartTime.HasValue ? NormalizedConfig.FormatDatetime(timeObject.StartTime.Value) : null,
                EndTime = timeObject.EndTime.HasValue ? NormalizedConfig.FormatDatetime(timeObject.EndTime.Value) : null
            };
        }
    }
}
