using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Modelling
{
    public class ModellingNamingConvention
    {
        [JsonProperty("networkAreaRequired"), JsonPropertyName("networkAreaRequired")]
        public bool NetworkAreaRequired { get; set; } = false;

        [JsonProperty("useAppPart"), JsonPropertyName("useAppPart")]
        public bool UseAppPart { get; set; } = false;

        [JsonProperty("fixedPartLength"), JsonPropertyName("fixedPartLength")]
        public int FixedPartLength { get; set; }

        [JsonProperty("freePartLength"), JsonPropertyName("freePartLength")]
        public int FreePartLength { get; set; }

        [JsonProperty("networkAreaPattern"), JsonPropertyName("networkAreaPattern")]
        public string NetworkAreaPattern { get; set; } = "";

        [JsonProperty("appRolePattern"), JsonPropertyName("appRolePattern")]
        public string AppRolePattern { get; set; } = "";

        [JsonProperty("applicationZone"), JsonPropertyName("applicationZone")]
        public string AppZone { get; set; } = "";

        [JsonProperty("appServerPrefix"), JsonPropertyName("appServerPrefix")]
        public string? AppServerPrefix { get; set; } = "";

        [JsonProperty("networkPrefix"), JsonPropertyName("networkPrefix")]
        public string? NetworkPrefix { get; set; } = "";

        [JsonProperty("ipRangePrefix"), JsonPropertyName("ipRangePrefix")]
        public string? IpRangePrefix { get; set; } = "";

        /// <summary>
        /// Deserializes a stored naming convention and repairs values that older or hand edited configurations may contain.
        /// </summary>
        /// <param name="json">serialized naming convention taken from the config</param>
        /// <returns>a normalized naming convention, never null</returns>
        public static ModellingNamingConvention FromJson(string? json)
        {
            ModellingNamingConvention namingConvention = string.IsNullOrWhiteSpace(json) ? new() :
                System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(json) ?? new();
            namingConvention.Normalize();
            return namingConvention;
        }

        /// <summary>
        /// Replaces null patterns and negative lengths that a stored configuration may contain,
        /// so that all consumers can rely on non null pattern strings.
        /// </summary>
        public void Normalize()
        {
            NetworkAreaPattern ??= "";
            AppRolePattern ??= "";
            AppZone ??= "";
            AppServerPrefix ??= "";
            NetworkPrefix ??= "";
            IpRangePrefix ??= "";
            FixedPartLength = Math.Max(FixedPartLength, 0);
            FreePartLength = Math.Max(FreePartLength, 0);
        }

        /// <summary>
        /// Checks that the fixed part is longer than the network area pattern.
        /// At equal length the fixed part consists of the pattern alone, so that every area is
        /// converted into the same app role fixed part and its area specific end is lost.
        /// </summary>
        /// <returns>true if the fixed part keeps at least one area specific position</returns>
        public bool IsFixedPartLengthValid()
        {
            return FixedPartLength > (NetworkAreaPattern?.Length ?? 0);
        }

        /// <summary>
        /// Checks that the app role pattern has exactly the length of the network area pattern.
        /// A longer pattern pushes the area specific end out of the fixed part, a shorter one leaves the
        /// converted identifier too short, so that it is padded with a filler and no longer maps back to its area.
        /// </summary>
        /// <returns>true if the app role pattern replaces the network area pattern without shifting the rest</returns>
        public bool IsAppRolePatternLengthValid()
        {
            return (AppRolePattern?.Length ?? 0) == (NetworkAreaPattern?.Length ?? 0);
        }

        /// <summary>
        /// Checks that converting an area identifier into an app role identifier keeps the area specific content.
        /// Conventions without network areas are always valid, as no conversion takes place for them.
        /// </summary>
        /// <returns>true if area identifiers can be converted into app role identifiers</returns>
        public bool IsAreaConversionValid()
        {
            return !NetworkAreaRequired || (IsFixedPartLengthValid() && IsAppRolePatternLengthValid());
        }
    }
}
