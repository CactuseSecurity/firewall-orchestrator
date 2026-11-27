using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Data.Flow;

namespace FWO.Data.Workflow
{
    public static class FlowIntegrationObjectSelectionOptions
    {
        public const string FromFlowDb = "FromFlowDb";
        public const string Manually = "Manually";
        public const string Both = "Both";

        public static readonly List<string> All =
        [
            FromFlowDb,
            Manually,
            Both
        ];
    }

    public static class FlowIntegrationTimePrecisionOptions
    {
        public const string Date = "date";
        public const string Hours = "hours";
        public const string Minutes = "minutes";
        public const string Seconds = "seconds";

        public static readonly List<string> All =
        [
            Date,
            Hours,
            Minutes,
            Seconds
        ];
    }

    /// <summary>
    /// Defines which validity bounds a flow time object may carry.
    /// </summary>
    public static class FlowIntegrationTimeObjectBoundsOptions
    {
        /// <summary>Time objects are defined by a start and an end date.</summary>
        public const string StartAndEnd = "start_and_end";

        /// <summary>Time objects are defined by their end date only; a start date is never stored.</summary>
        public const string EndOnly = "end_only";

        /// <summary>All supported bounds options, in display order.</summary>
        public static readonly IReadOnlyList<string> All =
        [
            StartAndEnd,
            EndOnly
        ];
    }

    public class FlowIntegrationConfig
    {
        [JsonPropertyName("select_objects")]
        public string SelectObjects { get; set; } = FlowIntegrationObjectSelectionOptions.Both;

        [JsonPropertyName("select_services")]
        public string SelectServices { get; set; } = FlowIntegrationObjectSelectionOptions.Both;

        [JsonPropertyName("select_time_objects")]
        public string SelectTimeObjects { get; set; } = FlowIntegrationObjectSelectionOptions.Both;

        [JsonPropertyName("time_object_precision")]
        public string TimeObjectPrecision { get; set; } = FlowIntegrationTimePrecisionOptions.Seconds;

        [JsonPropertyName("time_object_bounds")]
        public string TimeObjectBounds { get; set; } = FlowIntegrationTimeObjectBoundsOptions.StartAndEnd;

        /// <summary>
        /// True when flow time objects are defined by their end date only.
        /// </summary>
        [JsonIgnore]
        public bool IsEndOnly => TimeObjectBounds == FlowIntegrationTimeObjectBoundsOptions.EndOnly;

        /// <summary>
        /// Whether a Flow time object may be picked from the catalog: with end-only time objects only
        /// objects without a start date are offered.
        /// </summary>
        /// <param name="timeObject">The catalog time object.</param>
        /// <returns>True when the time object fits the configured bounds.</returns>
        public bool IsSelectableTimeObject(FlowTimeObject timeObject)
        {
            return !IsEndOnly || !timeObject.StartTime.HasValue;
        }

        /// <summary>
        /// Returns the start date to store for a time object: null when only end dates are allowed,
        /// otherwise the requested start date unchanged.
        /// </summary>
        /// <param name="requestedStart">The start date entered or received.</param>
        /// <returns>The effective start date.</returns>
        public DateTime? GetEffectiveStartTime(DateTime? requestedStart)
        {
            return IsEndOnly ? null : requestedStart;
        }

        public static FlowIntegrationConfig Parse(string? serializedConfig)
        {
            FlowIntegrationConfig config;
            if (string.IsNullOrWhiteSpace(serializedConfig))
            {
                config = new();
            }
            else
            {
                try
                {
                    config = JsonSerializer.Deserialize<FlowIntegrationConfig>(serializedConfig) ?? new();
                }
                catch (JsonException)
                {
                    config = new();
                }
            }
            config.SelectObjects = NormalizeSelectObjects(config.SelectObjects);
            config.SelectServices = NormalizeSelectObjects(config.SelectServices);
            config.SelectTimeObjects = NormalizeSelectObjects(config.SelectTimeObjects);
            config.TimeObjectPrecision = NormalizeTimeObjectPrecision(config.TimeObjectPrecision);
            config.TimeObjectBounds = NormalizeTimeObjectBounds(config.TimeObjectBounds);
            return config;
        }

        public string ToConfigValue()
        {
            SelectObjects = NormalizeSelectObjects(SelectObjects);
            SelectServices = NormalizeSelectObjects(SelectServices);
            SelectTimeObjects = NormalizeSelectObjects(SelectTimeObjects);
            TimeObjectPrecision = NormalizeTimeObjectPrecision(TimeObjectPrecision);
            TimeObjectBounds = NormalizeTimeObjectBounds(TimeObjectBounds);
            return JsonSerializer.Serialize(this);
        }

        private static string NormalizeSelectObjects(string? selectObjects)
        {
            return FlowIntegrationObjectSelectionOptions.All.Contains(selectObjects ?? "")
                ? selectObjects!
                : FlowIntegrationObjectSelectionOptions.Both;
        }

        private static string NormalizeTimeObjectPrecision(string? precision)
        {
            return FlowIntegrationTimePrecisionOptions.All.Contains(precision ?? "")
                ? precision!
                : FlowIntegrationTimePrecisionOptions.Seconds;
        }

        private static string NormalizeTimeObjectBounds(string? bounds)
        {
            return FlowIntegrationTimeObjectBoundsOptions.All.Contains(bounds ?? "")
                ? bounds!
                : FlowIntegrationTimeObjectBoundsOptions.StartAndEnd;
        }
    }
}
