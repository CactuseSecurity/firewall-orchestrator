using FWO.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    /// <summary>
    /// Parses the configured zone name patterns and decides whether a flow network group is a zone group.
    /// </summary>
    public static class FlowZoneGroupMatcher
    {
        private const string kLogTitle = "Flow Zone Groups";

        private static readonly JsonSerializerOptions kSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Parses the configured zone name patterns.
        /// Entries with an unusable match type, an empty value or a duplicate of an earlier entry are dropped
        /// individually, only a value that is no valid JSON array yields an empty list.
        /// Every dropped entry and every parse failure is logged.
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
                List<RawFlowZoneGroupPattern?> rawPatterns =
                    JsonSerializer.Deserialize<List<RawFlowZoneGroupPattern?>>(serializedPatterns, kSerializerOptions) ?? [];
                List<FlowZoneGroupPattern> convertedPatterns = ConvertRawPatterns(rawPatterns);
                List<FlowZoneGroupPattern> normalizedPatterns = Normalize(convertedPatterns);
                LogDroppedPatterns(convertedPatterns.Count - normalizedPatterns.Count);
                return normalizedPatterns;
            }
            catch (JsonException exception)
            {
                Log.WriteWarning(kLogTitle,
                    $"Configured zone name patterns are invalid JSON, no group is treated as a zone. {exception.Message}");
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
            SplitPatterns(patterns, out List<FlowZoneGroupPattern> normalizedPatterns, out _);
            return normalizedPatterns;
        }

        /// <summary>
        /// Returns the patterns that <see cref="Normalize"/> would drop because an earlier entry already matches
        /// the same names, so a caller can report them instead of losing them silently.
        /// </summary>
        /// <param name="patterns">The patterns to check.</param>
        /// <returns>The duplicate patterns in configuration order.</returns>
        public static List<FlowZoneGroupPattern> FindDuplicates(IEnumerable<FlowZoneGroupPattern>? patterns)
        {
            SplitPatterns(patterns, out _, out List<FlowZoneGroupPattern> duplicatePatterns);
            return duplicatePatterns;
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

        private static void SplitPatterns(IEnumerable<FlowZoneGroupPattern>? patterns,
            out List<FlowZoneGroupPattern> normalizedPatterns, out List<FlowZoneGroupPattern> duplicatePatterns)
        {
            normalizedPatterns = [];
            duplicatePatterns = [];
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

                if (seenPatternKeys.Add(BuildPatternKey(normalizedPattern)))
                {
                    normalizedPatterns.Add(normalizedPattern);
                }
                else
                {
                    duplicatePatterns.Add(normalizedPattern);
                }
            }
        }

        private static string BuildPatternKey(FlowZoneGroupPattern pattern)
        {
            string comparableValue = pattern.CaseSensitive ? pattern.Value : pattern.Value.ToUpperInvariant();
            return $"{pattern.MatchType}|{pattern.CaseSensitive}|{comparableValue}";
        }

        private static List<FlowZoneGroupPattern> ConvertRawPatterns(List<RawFlowZoneGroupPattern?> rawPatterns)
        {
            List<FlowZoneGroupPattern> convertedPatterns = [];

            foreach (RawFlowZoneGroupPattern? rawPattern in rawPatterns)
            {
                if (rawPattern == null)
                {
                    continue;
                }

                if (!TryConvertMatchType(rawPattern.MatchType, out FlowZoneNameMatchType matchType))
                {
                    Log.WriteWarning(kLogTitle,
                        $"Dropping zone name pattern '{rawPattern.Value}' with unusable match type '{rawPattern.MatchType}'.");
                    continue;
                }

                convertedPatterns.Add(new FlowZoneGroupPattern
                {
                    MatchType = matchType,
                    CaseSensitive = rawPattern.CaseSensitive,
                    Value = rawPattern.Value ?? ""
                });
            }

            return convertedPatterns;
        }

        private static bool TryConvertMatchType(JsonElement rawMatchType, out FlowZoneNameMatchType matchType)
        {
            matchType = FlowZoneNameMatchType.Suffix;

            return rawMatchType.ValueKind switch
            {
                JsonValueKind.Undefined => true,
                JsonValueKind.String => Enum.TryParse(rawMatchType.GetString(), true, out matchType) && Enum.IsDefined(matchType),
                JsonValueKind.Number => TryConvertNumericMatchType(rawMatchType, out matchType),
                _ => false
            };
        }

        private static bool TryConvertNumericMatchType(JsonElement rawMatchType, out FlowZoneNameMatchType matchType)
        {
            matchType = FlowZoneNameMatchType.Suffix;

            if (!rawMatchType.TryGetInt32(out int numericMatchType))
            {
                return false;
            }

            matchType = (FlowZoneNameMatchType)numericMatchType;
            return Enum.IsDefined(matchType);
        }

        private static void LogDroppedPatterns(int droppedPatternCount)
        {
            if (droppedPatternCount > 0)
            {
                Log.WriteWarning(kLogTitle,
                    $"Dropped {droppedPatternCount} empty or duplicate zone name pattern(s) from the configuration.");
            }
        }

        /// <summary>
        /// Tolerant representation of a configured pattern, so a single unusable entry cannot discard the whole list.
        /// </summary>
        private sealed class RawFlowZoneGroupPattern
        {
            [JsonPropertyName("matchType")]
            public JsonElement MatchType { get; set; }

            [JsonPropertyName("caseSensitive")]
            public bool CaseSensitive { get; set; }

            [JsonPropertyName("value")]
            public string? Value { get; set; }
        }
    }
}
