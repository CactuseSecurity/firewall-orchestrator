using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Report
{
    public enum LabelFilterMode
    {
        not_existing,
        existing,
        value,
        display_only
    }

    public class LabelFilter
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("mode"), JsonPropertyName("mode")]
        public LabelFilterMode Mode { get; set; } = LabelFilterMode.existing;

        [JsonProperty("value"), JsonPropertyName("value")]
        public string Value { get; set; } = "";

        public LabelFilter()
        { }

        public LabelFilter(LabelFilter labelFilter)
        {
            Name = labelFilter.Name;
            Mode = labelFilter.Mode;
            Value = labelFilter.Value;
        }
    }
}
