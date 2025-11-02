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
                RuleUid = violation.RuleUid,
                MgmtUid = violation.MgmtUid,
                FoundDate = violation.FoundDate,
                RemovedDate = violation.RemovedDate,
                Details = violation.Details,
                RiskScore = violation.RiskScore,
                PolicyId = violation.PolicyId,
                CriterionId = violation.CriterionId
            };
        }

        public ComplianceViolationType ParseViolationType(ComplianceCriterion? criterion)
        {
            if (criterion == null)
            {
                return ComplianceViolationType.None;
            }
            
            switch (criterion.CriterionType)
            {
                case "Matrix":
                    return ComplianceViolationType.MatrixViolation;

                case "Assessability":
                    return ComplianceViolationType.NotAssessable;

                case "ForbiddenService":
                    return ComplianceViolationType.ServiceViolation;

                    // TODO : implement for all criterion types

                default:
                    return ComplianceViolationType.None;
            }
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
        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string RuleUid { get; set; } = "";
        [JsonProperty("mgmt_uid"), JsonPropertyName("mgmt_uid")]
        public string MgmtUid { get; set; } = "";
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
        [JsonProperty("criterion"), JsonPropertyName("criterion")]
        public ComplianceCriterion? Criterion { get; set; }
    }




}


