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

        [JsonProperty("handlerDisplayMode"), JsonPropertyName("handlerDisplayMode")]
        public TicketHandlerDisplayMode HandlerDisplayMode { get; set; } = TicketHandlerDisplayMode.None;

        [JsonProperty("accessTask"), JsonPropertyName("accessTask")]
        public AccessTaskFieldVisibilityConfig AccessTask { get; set; } = new();

        [JsonProperty("objectTask"), JsonPropertyName("objectTask")]
        public ObjectTaskFieldVisibilityConfig ObjectTask { get; set; } = new();

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
                config.ObjectTask ??= new();
                return config;
            }
            catch (System.Text.Json.JsonException)
            {
                return new();
            }
        }

        /// <summary>
        /// Parses the field visibility for displaying and editing tickets and applies the flow integration:
        /// with end-only time objects the valid-from field is hidden regardless of the stored setting.
        /// Never use the result to edit and store the visibility configuration itself.
        /// </summary>
        /// <param name="serializedConfig">The JSON visibility configuration.</param>
        /// <param name="serializedFlowIntegration">The JSON flow integration configuration.</param>
        /// <returns>The effective visibility configuration.</returns>
        public static TicketFieldVisibilityConfig ParseEffective(string? serializedConfig, string? serializedFlowIntegration)
        {
            TicketFieldVisibilityConfig config = Parse(serializedConfig);
            if (FlowIntegrationConfig.Parse(serializedFlowIntegration).IsEndOnly)
            {
                config.AccessTask.ShowValidFrom = false;
            }
            return config;
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

        [JsonProperty("ruleAction"), JsonPropertyName("ruleAction")]
        public bool ShowRuleAction { get; set; } = true;

        [JsonProperty("tracking"), JsonPropertyName("tracking")]
        public bool ShowTracking { get; set; } = true;

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

        /// <summary>Shows the task title in the approval views; the request dialog always shows it, as it is required.</summary>
        [JsonProperty("title"), JsonPropertyName("title")]
        public bool ShowTitle { get; set; } = true;

        /// <summary>Shows the current handler in the approval views.</summary>
        [JsonProperty("handler"), JsonPropertyName("handler")]
        public bool ShowHandler { get; set; } = true;

        /// <summary>Shows the assigned group in the approval views.</summary>
        [JsonProperty("assigned"), JsonPropertyName("assigned")]
        public bool ShowAssigned { get; set; } = true;
    }

    /// <summary>
    /// Configures which fields of object and group request tasks are displayed in the approval views.
    /// </summary>
    public class ObjectTaskFieldVisibilityConfig
    {
        /// <summary>Shows the current handler.</summary>
        [JsonProperty("handler"), JsonPropertyName("handler")]
        public bool ShowHandler { get; set; } = true;

        /// <summary>Shows the assigned group.</summary>
        [JsonProperty("assigned"), JsonPropertyName("assigned")]
        public bool ShowAssigned { get; set; } = true;

        /// <summary>Shows the management.</summary>
        [JsonProperty("management"), JsonPropertyName("management")]
        public bool ShowManagement { get; set; } = true;

        /// <summary>Shows the reason.</summary>
        [JsonProperty("reason"), JsonPropertyName("reason")]
        public bool ShowReason { get; set; } = true;
    }
}
