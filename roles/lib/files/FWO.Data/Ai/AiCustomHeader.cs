using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Ai
{
    /// <summary>
    /// Additional HTTP header the AI harness sends with every request to the AI provider, e.g. the profile an API gateway
    /// in front of the model needs (<see cref="AiSettings.CustomHeaders"/>). Name and value are stored in plain text in the
    /// global config, so the value must not be a secret.
    /// </summary>
    public class AiCustomHeader
    {
        /// <summary>Name of the header, e.g. x-gateway-profile.</summary>
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>Value of the header.</summary>
        [JsonProperty("value"), JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }
}
