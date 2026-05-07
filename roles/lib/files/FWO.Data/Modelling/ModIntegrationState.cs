using Newtonsoft.Json;
using System.Globalization;
using System.Text.Json.Serialization;

namespace FWO.Data.Modelling
{
    public class ModIntegrationState
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("include_into_request"), JsonPropertyName("include_into_request")]
        public bool IncludeIntoRequest { get; set; }
    }

    public static class ModIntegrationStateConfig
    {
        public const string DefaultMarker = "ImplementationState";
        public const string StateTimestampSeparator = "|";

        public static string TimestampMarker(string marker)
        {
            return $"{EffectiveMarker(marker)}SetAt";
        }

        public static HashSet<string> IncludedRequestStateNames(string configValue)
        {
            return [.. Parse(configValue)
                .Where(state => state.IncludeIntoRequest)
                .Select(state => state.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))];
        }

        public static bool IsIncludedForRequest(string? stateName, HashSet<string> includedRequestStateNames)
        {
            return string.IsNullOrWhiteSpace(stateName) || includedRequestStateNames.Contains(stateName);
        }

        public static bool RequiresChangeSinceMarker(string? stateName, HashSet<string> includedRequestStateNames)
        {
            return !string.IsNullOrWhiteSpace(stateName) && includedRequestStateNames.Contains(stateName);
        }

        public static bool IsMarkedCommentIncludedForRequest(string? comment, string marker, HashSet<string> includedRequestStateNames)
        {
            return IsIncludedForRequest(GetMarkedCommentValue(comment, marker), includedRequestStateNames);
        }

        public static string GetMarkedCommentValue(string? comment, string marker)
        {
            string markerPrefix = $"{EffectiveMarker(marker)}:";
            return GetStateName(GetCommentMarkerValue(comment, markerPrefix));
        }

        public static DateTime? GetMarkedCommentTimestamp(string? comment, string marker)
        {
            string markerPrefix = $"{EffectiveMarker(marker)}:";
            string markerValue = GetCommentMarkerValue(comment, markerPrefix);
            DateTime? timestamp = GetStateTimestamp(markerValue);
            if (timestamp != null)
            {
                return timestamp;
            }
            string timestampMarkerPrefix = $"{TimestampMarker(marker)}:";
            string timestampValue = GetCommentMarkerValue(comment, timestampMarkerPrefix);
            return ParseTimestamp(timestampValue);
        }

        public static string BuildStateValue(string stateName, string stateSetAt)
        {
            return $"{stateName.Trim()} {StateTimestampSeparator} {stateSetAt}";
        }

        public static string GetStateName(string? markedValue)
        {
            return SplitStateValue(markedValue).StateName;
        }

        public static DateTime? GetStateTimestamp(string? markedValue)
        {
            return ParseTimestamp(SplitStateValue(markedValue).Timestamp);
        }

        public static DateTime? ParseTimestamp(string? timestampValue)
        {
            return DateTime.TryParse(timestampValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime timestamp)
                ? timestamp.ToUniversalTime()
                : null;
        }

        private static string GetCommentMarkerValue(string? comment, string markerPrefix)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return "";
            }

            foreach (string line in comment.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith(markerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedLine[markerPrefix.Length..].Trim();
                }
            }
            return "";
        }

        private static (string StateName, string Timestamp) SplitStateValue(string? markedValue)
        {
            if (string.IsNullOrWhiteSpace(markedValue))
            {
                return ("", "");
            }
            string[] parts = markedValue.Split(StateTimestampSeparator, 2, StringSplitOptions.TrimEntries);
            return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "");
        }

        public static string EffectiveMarker(string marker)
        {
            return string.IsNullOrWhiteSpace(marker) ? DefaultMarker : marker.Trim();
        }

        public static List<ModIntegrationState> Parse(string configValue)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return [];
            }
            return System.Text.Json.JsonSerializer.Deserialize<List<ModIntegrationState>>(configValue) ?? [];
        }

        public static string ToConfigValue(IEnumerable<ModIntegrationState> states)
        {
            return System.Text.Json.JsonSerializer.Serialize(states
                .Where(state => !string.IsNullOrWhiteSpace(state.Name))
                .Select(state => new ModIntegrationState
                {
                    Name = state.Name.Trim(),
                    IncludeIntoRequest = state.IncludeIntoRequest
                })
                .DistinctBy(state => state.Name)
                .ToList());
        }
    }
}
