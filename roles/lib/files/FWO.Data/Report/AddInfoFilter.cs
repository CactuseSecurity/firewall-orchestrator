using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Report
{
    public enum AddInfoFilterMode
    {
        not_existing,
        existing,
        value,
        display_only
    }

    public class AddInfoFilter
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("mode"), JsonPropertyName("mode")]
        public AddInfoFilterMode Mode { get; set; } = AddInfoFilterMode.existing;

        [JsonProperty("value"), JsonPropertyName("value")]
        public string Value { get; set; } = "";

        public AddInfoFilter()
        { }

        public AddInfoFilter(AddInfoFilter? addInfoFilter)
        {
            Name = addInfoFilter?.Name ?? "";
            Mode = addInfoFilter?.Mode ?? AddInfoFilterMode.existing;
            Value = addInfoFilter?.Value ?? "";
        }
    }
}
