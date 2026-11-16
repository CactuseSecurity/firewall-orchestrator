using System.Globalization;
using System.Text.Json;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Identifies the workflow participant that introduced a time object.
    /// </summary>
    public enum TimeObjectOrigin
    {
        /// <summary>The original requester.</summary>
        Requester,
        /// <summary>An approver or responsible approval department.</summary>
        Approver
    }

    /// <summary>
    /// Parses configured time-object suffixes and builds consistent display names.
    /// </summary>
    public static class TimeObjectNamingHelper
    {
        /// <summary>
        /// Parses the serialized origin-to-suffix configuration, using empty suffixes for missing or invalid values.
        /// </summary>
        /// <param name="serializedSuffixes">The serialized suffix configuration.</param>
        /// <returns>A case-insensitive dictionary containing the supported origins.</returns>
        public static Dictionary<string, string> ParseSuffixes(string? serializedSuffixes)
        {
            Dictionary<string, string> suffixes = new(StringComparer.OrdinalIgnoreCase)
            {
                [TimeObjectOrigin.Requester.ToString().ToLowerInvariant()] = "",
                [TimeObjectOrigin.Approver.ToString().ToLowerInvariant()] = ""
            };
            if (!string.IsNullOrWhiteSpace(serializedSuffixes))
            {
                try
                {
                    Dictionary<string, string>? configured = JsonSerializer.Deserialize<Dictionary<string, string>>(serializedSuffixes);
                    if (configured != null)
                    {
                        foreach ((string key, string value) in configured)
                        {
                            if (Enum.TryParse<TimeObjectOrigin>(key, true, out TimeObjectOrigin origin))
                            {
                                suffixes[origin.ToString().ToLowerInvariant()] = value ?? "";
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Keep the empty defaults for malformed or legacy configuration.
                }
            }
            return suffixes;
        }

        /// <summary>
        /// Adds the configured suffix for an origin unless the name already ends in any configured suffix.
        /// </summary>
        /// <param name="baseName">The existing time-object name.</param>
        /// <param name="origin">The origin to use when a suffix is needed.</param>
        /// <param name="suffixes">The configured origin suffixes.</param>
        /// <returns>The unchanged or suffixed name.</returns>
        public static string BuildDisplayName(string? baseName, TimeObjectOrigin? origin,
            IReadOnlyDictionary<string, string>? suffixes)
        {
            if (string.IsNullOrWhiteSpace(baseName) || suffixes == null)
            {
                return baseName ?? "";
            }

            if (HasConfiguredSuffix(baseName, suffixes))
            {
                return baseName;
            }

            if (!origin.HasValue)
            {
                return baseName;
            }

            string key = origin.Value.ToString().ToLowerInvariant();
            return suffixes.TryGetValue(key, out string? suffix) && !string.IsNullOrEmpty(suffix)
                ? baseName + suffix
                : baseName;
        }

        private static bool HasConfiguredSuffix(string baseName, IReadOnlyDictionary<string, string> suffixes)
        {
            return suffixes.Values
                .Where(suffix => !string.IsNullOrEmpty(suffix))
                .Any(suffix => baseName.EndsWith(suffix, StringComparison.Ordinal));
        }

        /// <summary>
        /// Builds the conventional date-based fallback name using the configured precision.
        /// </summary>
        /// <param name="start">The start of the validity interval.</param>
        /// <param name="end">The end of the validity interval.</param>
        /// <param name="precision">The configured display precision.</param>
        /// <returns>A date-based name, including open-bound markers where applicable.</returns>
        public static string BuildDateBasedName(DateTime? start, DateTime? end, string precision)
        {
            string Format(DateTime value) => precision switch
            {
                FlowIntegrationTimePrecisionOptions.Date => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FlowIntegrationTimePrecisionOptions.Hours => value.ToString("yyyy-MM-dd HH 'h'", CultureInfo.InvariantCulture),
                FlowIntegrationTimePrecisionOptions.Minutes => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _ => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            };

            if (start.HasValue && end.HasValue)
            {
                return $"{Format(start.Value)} - {Format(end.Value)}";
            }
            if (start.HasValue)
            {
                return $">= {Format(start.Value)}";
            }
            return end.HasValue ? $"< {Format(end.Value)}" : "";
        }
    }

    /// <summary>
    /// Formats time-object values according to the configured precision.
    /// </summary>
    public static class TimeObjectDisplayHelper
    {
        /// <summary>
        /// Formats one validity boundary and converts a date-precision end boundary to its inclusive date.
        /// </summary>
        /// <param name="value">The stored validity boundary.</param>
        /// <param name="precision">The configured time-object precision.</param>
        /// <param name="isEndDate">Whether the boundary is the end of the validity interval.</param>
        /// <param name="dateFormat">The date portion format used by the caller.</param>
        /// <returns>The formatted value, or an empty string when no value is present.</returns>
        public static string FormatValue(DateTime? value, string precision, bool isEndDate, string dateFormat)
        {
            if (!value.HasValue)
            {
                return "";
            }

            DateTime date = value.Value;
            string normalizedPrecision = precision.Trim().ToLowerInvariant();
            if (normalizedPrecision == FlowIntegrationTimePrecisionOptions.Date)
            {
                if (isEndDate && date.TimeOfDay == TimeSpan.Zero)
                {
                    date = date.AddDays(-1);
                }
                return date.ToString(dateFormat, CultureInfo.InvariantCulture);
            }

            return normalizedPrecision switch
            {
                FlowIntegrationTimePrecisionOptions.Hours => date.ToString($"{dateFormat} H 'h'", CultureInfo.InvariantCulture),
                FlowIntegrationTimePrecisionOptions.Minutes => date.ToString($"{dateFormat} HH:mm", CultureInfo.InvariantCulture),
                _ => date.ToString($"{dateFormat} HH:mm:ss", CultureInfo.InvariantCulture)
            };
        }
    }
}
