using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Configures which ticket fields are displayed in the ticket dialog.
    /// </summary>
    public class TicketFieldVisibilityConfig
    {
        [JsonProperty("requester"), JsonPropertyName("requester")]
        public bool ShowRequester { get; set; } = true;

        [JsonProperty("priority"), JsonPropertyName("priority")]
        public bool ShowPriority { get; set; } = true;

        [JsonProperty("deadline"), JsonPropertyName("deadline")]
        public bool ShowDeadline { get; set; } = true;

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public bool ShowReason { get; set; } = true;

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public bool ShowComments { get; set; } = false;

        [JsonProperty("accessTask"), JsonPropertyName("accessTask")]
        public AccessTaskFieldVisibilityConfig AccessTask { get; set; } = new();

        /// <summary>
        /// Parses a serialized ticket and access-task field visibility configuration.
        /// </summary>
        /// <param name="serializedConfig">The JSON configuration, or <see langword="null"/> for defaults.</param>
        /// <returns>The parsed configuration, or the default configuration when the input is empty or invalid.</returns>
        public static TicketFieldVisibilityConfig Parse(string? serializedConfig)
        {
            if (string.IsNullOrWhiteSpace(serializedConfig))
            {
                return new();
            }

            try
            {
                TicketFieldVisibilityConfig config = System.Text.Json.JsonSerializer.Deserialize<TicketFieldVisibilityConfig>(serializedConfig) ?? new();
                config.AccessTask ??= new();
                return config;
            }
            catch (System.Text.Json.JsonException)
            {
                return new();
            }
        }

        /// <summary>
        /// Serializes this field visibility configuration for storage in workflow settings.
        /// </summary>
        /// <returns>The JSON representation of this configuration.</returns>
        public string ToConfigValue()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// Configures which access-request-task fields are displayed in the task dialog.
    /// </summary>
    public class AccessTaskFieldVisibilityConfig
    {
        [JsonProperty("management"), JsonPropertyName("management")]
        public bool ShowManagement { get; set; } = true;

        [JsonProperty("owner"), JsonPropertyName("owner")]
        public bool ShowOwner { get; set; } = true;

        [JsonProperty("validFrom"), JsonPropertyName("validFrom")]
        public bool ShowValidFrom { get; set; } = true;

        [JsonProperty("validTo"), JsonPropertyName("validTo")]
        public bool ShowValidTo { get; set; } = true;

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public bool ShowReason { get; set; } = true;

        [JsonProperty("freeText"), JsonPropertyName("freeText")]
        public bool ShowFreeText { get; set; } = false;

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public bool ShowComments { get; set; } = true;
    }
}
