using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// What a path analysis workflow action does with the devices it finds.
    /// </summary>
    public enum PathAnalysisOptions
    {
        /// <summary>Stores the found devices as the device list of the request task.</summary>
        WriteToDeviceList,
        /// <summary>Only shows the found devices to the user.</summary>
        DisplayFoundDevices
    }

    /// <summary>
    /// Parameters of a path analysis workflow action, stored as JSON in the action's external parameters.
    /// </summary>
    public class PathAnalysisActionParams
    {
        /// <summary>
        /// Gets or sets what the action does with the found devices. Defaults to showing them.
        /// </summary>
        [JsonProperty("option"), JsonPropertyName("option")]
        public PathAnalysisOptions Option { get; set; } = PathAnalysisOptions.DisplayFoundDevices;
    }
}
