using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NormalizedRulebase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("mgm_uid"), JsonPropertyName("mgm_uid")]
        public string MgmUid { get; set; } = "";

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; }

        [JsonProperty("Rules"), JsonPropertyName("Rules")]
        public Dictionary<string, NormalizedRule> Rules { get; set; } = [];

        /// <summary>
        /// Creates a NormalizedRulebase from a Rulebase.
        /// </summary>
        /// <param name="rulebase">The Rulebase to normalize.</param>
        /// <param name="mgmUid">The management UID.</param>
        /// <returns>A normalized Rulebase.</returns>
        /// <exception cref="ArgumentException">Thrown if any rule in the rulebase lacks a UID.</exception>
        public static NormalizedRulebase FromRulebase(Rulebase rulebase, string mgmUid)
        {
            if (rulebase.Rules.Any(r => r.Uid == null))
            {
                throw new ArgumentException("All rules in the rulebase must have a UID.");
            }
            return new NormalizedRulebase
            {
                Id = null, // Id is omitted in normalized representation
                Uid = rulebase.Uid,
                Name = rulebase.Name,
                MgmUid = mgmUid,
                IsGlobal = rulebase.IsGlobal,
                Rules = rulebase.Rules.ToDictionary(
                    r => r.Uid!,
                    NormalizedRule.FromRule
                )
            };
        }
    }
}
