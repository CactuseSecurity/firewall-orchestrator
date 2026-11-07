using System.Text.Json.Serialization;
using FWO.Basics;
using Newtonsoft.Json;

namespace FWO.Data.Modelling
{
    public class ModellingHistoryEntry
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int? AppId { get; set; }

        [JsonProperty("module"), JsonPropertyName("module")]
        public string Module { get; set; } = GlobalConst.kModuleModelling;

        [JsonProperty("change_type"), JsonPropertyName("change_type")]
        public int ChangeType { get; set; }

        [JsonProperty("object_type"), JsonPropertyName("object_type")]
        public int ObjectType { get; set; }

        [JsonProperty("object_id"), JsonPropertyName("object_id")]
        public long ObjectId { get; set; }

        [JsonProperty("change_text"), JsonPropertyName("change_text")]
        public string ChangeText { get; set; } = "";

        [JsonProperty("changer"), JsonPropertyName("changer")]
        public string Changer { get; set; } = "";

        [JsonProperty("changer_id"), JsonPropertyName("changer_id")]
        public int? ChangerId { get; set; }

        /// <summary>
        /// User record of <see cref="ChangerId"/>, when the query selects it. Its name is the authenticated
        /// author, unlike <see cref="Changer"/>, which older rows may carry as free text of the writer.
        /// </summary>
        [JsonProperty("changer_user"), JsonPropertyName("changer_user")]
        public UiUser? ChangerUser { get; set; }

        /// <summary>
        /// Name of the author: the login name of the user behind <see cref="ChangerId"/> when it is known,
        /// otherwise the stored <see cref="Changer"/>, which names the automation for rows without a user.
        /// </summary>
        /// <returns>The author name, empty when none is recorded.</returns>
        public string GetAuthenticatedChangerName()
        {
            string? resolvedName = ChangerUser?.Name;
            if (!string.IsNullOrEmpty(resolvedName))
            {
                return resolvedName;
            }
            return Changer ?? "";
        }

        [JsonProperty("change_time"), JsonPropertyName("change_time")]
        public DateTime? ChangeTime { get; set; }

        [JsonProperty("change_source"), JsonPropertyName("change_source")]
        public string ChangeSource { get; set; } = GlobalConst.kManual;

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long? TicketId { get; set; }

        [JsonProperty("workflow_phase"), JsonPropertyName("workflow_phase")]
        public int? WorkflowPhase { get; set; }

        [JsonProperty("old_data"), JsonPropertyName("old_data")]
        public object? OldData { get; set; }

        [JsonProperty("new_data"), JsonPropertyName("new_data")]
        public object? NewData { get; set; }

        [JsonProperty("audit_proof_critical"), JsonPropertyName("audit_proof_critical")]
        public bool AuditProofCritical { get; set; }
    }
}
