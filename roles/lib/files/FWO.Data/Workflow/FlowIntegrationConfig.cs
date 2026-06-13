using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    public static class FlowIntegrationObjectSelectionOptions
    {
        public const string FromFlowDb = "from_flowdb";
        public const string Manually = "manually";
        public const string Both = "both";

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
            return config;
        }

        public string ToConfigValue()
        {
            SelectObjects = NormalizeSelectObjects(SelectObjects);
            SelectServices = NormalizeSelectObjects(SelectServices);
            SelectTimeObjects = NormalizeSelectObjects(SelectTimeObjects);
            TimeObjectPrecision = NormalizeTimeObjectPrecision(TimeObjectPrecision);
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
    }
}
