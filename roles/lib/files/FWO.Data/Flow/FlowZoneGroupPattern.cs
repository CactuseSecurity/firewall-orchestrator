using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    /// <summary>
    /// Supported ways of matching a flow network group name against a configured zone pattern.
    /// </summary>
    public enum FlowZoneNameMatchType
    {
        /// <summary>
        /// The group name has to end with the configured value.
        /// </summary>
        Suffix = 0,

        /// <summary>
        /// The group name has to start with the configured value.
        /// </summary>
        Prefix = 1,

        /// <summary>
        /// The group name has to contain the configured value.
        /// </summary>
        Contains = 2,

        /// <summary>
        /// The group name has to be equal to the configured value.
        /// </summary>
        Exact = 3
    }

    /// <summary>
    /// Single configured rule that decides whether a flow network group is treated as a zone.
    /// </summary>
    public class FlowZoneGroupPattern
    {
        /// <summary>
        /// Gets or sets the way the value is matched against a group name.
        /// </summary>
        [JsonPropertyName("matchType"), JsonConverter(typeof(JsonStringEnumConverter<FlowZoneNameMatchType>))]
        public FlowZoneNameMatchType MatchType { get; set; } = FlowZoneNameMatchType.Suffix;

        /// <summary>
        /// Gets or sets a value indicating whether the match is performed case sensitively.
        /// </summary>
        [JsonPropertyName("caseSensitive")]
        public bool CaseSensitive { get; set; }

        /// <summary>
        /// Gets or sets the literal value that is matched against a group name.
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }
}
