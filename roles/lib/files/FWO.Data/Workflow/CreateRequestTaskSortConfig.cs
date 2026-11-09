using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Configures create-request task sorting and split behavior.
    /// </summary>
    public class CreateRequestTaskSortConfig
    {
        [JsonProperty("group_create_priority"), JsonPropertyName("group_create_priority")]
        public int GroupCreatePriority { get; set; } = 0;

        [JsonProperty("group_modify_add_priority"), JsonPropertyName("group_modify_add_priority")]
        public int GroupModifyAddPriority { get; set; } = 1;

        [JsonProperty("access_priority"), JsonPropertyName("access_priority")]
        public int AccessPriority { get; set; } = 2;

        [JsonProperty("rule_modify_priority"), JsonPropertyName("rule_modify_priority")]
        public int RuleModifyPriority { get; set; } = 3;

        [JsonProperty("rule_delete_priority"), JsonPropertyName("rule_delete_priority")]
        public int RuleDeletePriority { get; set; } = 4;

        [JsonProperty("group_modify_remove_priority"), JsonPropertyName("group_modify_remove_priority")]
        public int GroupModifyRemovePriority { get; set; } = 5;

        [JsonProperty("group_delete_priority"), JsonPropertyName("group_delete_priority")]
        public int GroupDeletePriority { get; set; } = 6;

        [JsonProperty("allow_task_split"), JsonPropertyName("allow_task_split")]
        public bool AllowTaskSplit { get; set; } = true;

        public static CreateRequestTaskSortConfig Parse(string? serializedConfig)
        {
            if (string.IsNullOrWhiteSpace(serializedConfig))
            {
                return new CreateRequestTaskSortConfig();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CreateRequestTaskSortConfig>(serializedConfig) ?? new CreateRequestTaskSortConfig();
            }
            catch (System.Text.Json.JsonException)
            {
                return new CreateRequestTaskSortConfig();
            }
        }

        public string ToConfigValue()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }
    }
}
