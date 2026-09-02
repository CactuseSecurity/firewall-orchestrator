using System.Text.Json;

namespace FWO.Data.Flow
{
    /// <summary>
    /// Parses the configured zone name patterns and decides whether a flow network group is a zone group.
    /// </summary>
    public static class FlowZoneGroupMatcher
    {
        private static readonly JsonSerializerOptions kSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Parses the configured zone name patterns.
        /// Invalid, empty or duplicate entries are dropped, an unparsable value yields an empty list.
        /// </summary>
        /// <param name="serializedPatterns">The serialized pattern list taken from the configuration.</param>
        /// <returns>The usable patterns in configuration order.</returns>
        public static List<FlowZoneGroupPattern> ParsePatterns(string? serializedPatterns)
        {
            if (string.IsNullOrWhiteSpace(serializedPatterns))
            {
                return [];
            }

            try
            {
                List<FlowZoneGroupPattern> parsedPatterns =
                    JsonSerializer.Deserialize<List<FlowZoneGroupPattern>>(serializedPatterns, kSerializerOptions) ?? [];
                return Normalize(parsedPatterns);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Serializes the given zone name patterns for storage in the configuration.
        /// </summary>
        /// <param name="patterns">The patterns to store.</param>
        /// <returns>The serialized pattern list.</returns>
        public static string SerializePatterns(IEnumerable<FlowZoneGroupPattern>? patterns)
        {
            return JsonSerializer.Serialize(Normalize(patterns ?? []), kSerializerOptions);
        }

        /// <summary>
        /// Removes unusable and duplicate patterns while keeping the configured order.
        /// </summary>
        /// <param name="patterns">The patterns to normalize.</param>
        /// <returns>The normalized patterns.</returns>
        public static List<FlowZoneGroupPattern> Normalize(IEnumerable<FlowZoneGroupPattern>? patterns)
        {
            List<FlowZoneGroupPattern> normalizedPatterns = [];
            HashSet<string> seenPatternKeys = [];

            foreach (FlowZoneGroupPattern pattern in patterns ?? [])
            {
                if (pattern == null || string.IsNullOrWhiteSpace(pattern.Value) || !Enum.IsDefined(pattern.MatchType))
                {
                    continue;
                }

                FlowZoneGroupPattern normalizedPattern = new()
                {
                    MatchType = pattern.MatchType,
                    CaseSensitive = pattern.CaseSensitive,
                    Value = pattern.Value.Trim()
                };

                if (normalizedPattern.Value.Length > 0 && seenPatternKeys.Add(BuildPatternKey(normalizedPattern)))
                {
                    normalizedPatterns.Add(normalizedPattern);
                }
            }

            return normalizedPatterns;
        }

        /// <summary>
        /// Checks whether the given group name matches at least one of the configured zone patterns.
        /// Without any configured pattern no group is treated as a zone.
        /// </summary>
        /// <param name="groupName">The flow network group name to check.</param>
        /// <param name="patterns">The configured zone patterns.</param>
        /// <returns>True if the name identifies a zone group.</returns>
        public static bool IsZoneGroupName(string? groupName, IEnumerable<FlowZoneGroupPattern>? patterns)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            return (patterns ?? []).Any(pattern => Matches(groupName, pattern));
        }

        /// <summary>
        /// Checks whether the given group name matches one single zone pattern.
        /// </summary>
        /// <param name="groupName">The flow network group name to check.</param>
        /// <param name="pattern">The zone pattern to apply.</param>
        /// <returns>True if the name matches the pattern.</returns>
        public static bool Matches(string? groupName, FlowZoneGroupPattern? pattern)
        {
            if (string.IsNullOrWhiteSpace(groupName) || pattern == null || string.IsNullOrWhiteSpace(pattern.Value))
            {
                return false;
            }

            string patternValue = pattern.Value.Trim();
            StringComparison comparison = pattern.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return pattern.MatchType switch
            {
                FlowZoneNameMatchType.Suffix => groupName.EndsWith(patternValue, comparison),
                FlowZoneNameMatchType.Prefix => groupName.StartsWith(patternValue, comparison),
                FlowZoneNameMatchType.Contains => groupName.Contains(patternValue, comparison),
                FlowZoneNameMatchType.Exact => string.Equals(groupName, patternValue, comparison),
                _ => false
            };
        }

        private static string BuildPatternKey(FlowZoneGroupPattern pattern)
        {
            string comparableValue = pattern.CaseSensitive ? pattern.Value : pattern.Value.ToUpperInvariant();
            return $"{pattern.MatchType}|{pattern.CaseSensitive}|{comparableValue}";
        }
    }
}
