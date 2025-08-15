using FWO.Basics.Interfaces;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class ComplianceViolation : ComplianceViolationBase, IComplianceViolation
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        public ComplianceViolationType Type { get; set; } = ComplianceViolationType.None;

        public static ComplianceViolation Copy(ComplianceViolation violation)
        {
            return new()
            {
                Id = violation.Id,
                RuleId = violation.RuleId,
                FoundDate = violation.FoundDate,
                RemovedDate = violation.RemovedDate,
                Details = violation.Details,
                RiskScore = violation.RiskScore,
                PolicyId = violation.PolicyId,
                CriterionId = violation.CriterionId
            };
        }
    }

    /// <summary>
    /// Insertable base class for compliance violations.
    /// This class is used to insert new compliance violations into the database.
    /// </summary>
    public class ComplianceViolationBase
    {
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public int RuleId { get; set; }
        [JsonProperty("found_date"), JsonPropertyName("found_date")]
        public DateTime FoundDate { get; set; } = DateTime.Now;
        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; } = null;
        [JsonProperty("details"), JsonPropertyName("details")]
        public string Details { get; set; } = "";
        [JsonProperty("risk_score"), JsonPropertyName("risk_score")]
        public long RiskScore { get; set; }
        [JsonProperty("policy_id"), JsonPropertyName("policy_id")]
        public int PolicyId { get; set; }
        [JsonProperty("criterion_id"), JsonPropertyName("criterion_id")]
        public int CriterionId { get; set; }
    }
}