using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Active rule owner mapping with the app id of the owner, the source of the App label of a rule.
    /// </summary>
    public class RuleOwnerApp
    {
        /// <summary>
        /// Id of the mapped rule.
        /// </summary>
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        /// <summary>
        /// The owner the rule is mapped to.
        /// </summary>
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public RuleOwnerAppOwner Owner { get; set; } = new();
    }

    /// <summary>
    /// App id of the owner of a rule owner mapping.
    /// </summary>
    public class RuleOwnerAppOwner
    {
        /// <summary>
        /// External app id of the owner (from the CMDB), null if the owner has none.
        /// </summary>
        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string? AppIdExternal { get; set; }
    }

    /// <summary>
    /// Active assignment of a label to a rule as a whole.
    /// </summary>
    public class RuleLabelRow
    {
        /// <summary>
        /// Id of the rule.
        /// </summary>
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        /// <summary>
        /// Id of the assigned label.
        /// </summary>
        [JsonProperty("label_id"), JsonPropertyName("label_id")]
        public long LabelId { get; set; }
    }

    /// <summary>
    /// An owner with its app id and its active app servers with their active App labels.
    /// </summary>
    public class OwnerAppServerAppLabels
    {
        /// <summary>
        /// External app id of the owner, null if the owner has none.
        /// </summary>
        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string? AppIdExternal { get; set; }

        /// <summary>
        /// The active app servers of the owner.
        /// </summary>
        [JsonProperty("owner_networks"), JsonPropertyName("owner_networks")]
        public List<AppServerAppLabels> AppServers { get; set; } = [];
    }

    /// <summary>
    /// An app server with its active App labels.
    /// </summary>
    public class AppServerAppLabels
    {
        /// <summary>
        /// Id of the app server (owner network).
        /// </summary>
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// The active App label assignments of the app server.
        /// </summary>
        [JsonProperty("owner_network_labels"), JsonPropertyName("owner_network_labels")]
        public List<OwnerNetworkLabel> AppLabels { get; set; } = [];
    }

    /// <summary>
    /// Label values returned by an upsert.
    /// </summary>
    public class LabelValueUpsertResult
    {
        /// <summary>
        /// The inserted or already existing label values.
        /// </summary>
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<LabelValue> Returning { get; set; } = [];
    }
}
