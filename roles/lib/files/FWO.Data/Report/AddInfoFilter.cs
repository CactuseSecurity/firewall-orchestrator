using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Report
{
    /// <summary>
    /// The user-facing mode names are shared across reporting contexts, but the concrete
    /// filtering semantics are defined by the report that interprets the filter.
    /// </summary>
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
