using FWO.Data;

namespace FWO.Test
{
    /// <summary>
    /// Builds the violation and rule fixtures shared by the compliance report test fixtures.
    /// </summary>
    internal static class ComplianceReportTestData
    {
        /// <summary>
        /// Builds a violation as the compliance report reads it back from the API.
        /// </summary>
        public static ComplianceViolation CreateMockComplianceViolation(int id = 0, int ruleId = 0, DateTime? foundDate = null, string details = "", int policyId = 0, ComplianceCriterion? criterion = null, ComplianceViolationType type = ComplianceViolationType.None)
        {
            if (string.IsNullOrEmpty(details))
            {
                details = $"Test violation {id}";
            }

            if (criterion == null)
            {
                criterion = new()
                {
                    Id = 0
                };
            }

            ComplianceViolation violation = new()
            {
                Id = id,
                RuleId = ruleId,
                FoundDate = foundDate ?? DateTime.Now,
                Details = details,
                RiskScore = 0,
                PolicyId = policyId,
                CriterionId = criterion.Id,
                Criterion = criterion
            };

            violation.Type = type;

            return violation;
        }

        /// <summary>
        /// Builds a matrix violation carrying the rule and management identity the diff pipeline matches on.
        /// </summary>
        public static ComplianceViolation CreateDiffViolation(
            int id,
            int ruleId,
            string ruleUid,
            DateTime? removedDate = null,
            DateTime? foundDate = null,
            bool isInitial = false,
            string mgmtUid = "mgmt-1")
        {
            return new ComplianceViolation
            {
                Id = id,
                RuleId = ruleId,
                RuleUid = ruleUid,
                MgmtUid = mgmtUid,
                FoundDate = foundDate ?? DateTime.Now.AddHours(-2),
                RemovedDate = removedDate,
                IsInitial = isInitial,
                Details = $"Violation {id}",
                Criterion = new ComplianceCriterion
                {
                    CriterionType = nameof(CriterionType.Matrix)
                },
                Type = ComplianceViolationType.MatrixViolation
            };
        }

        /// <summary>
        /// Builds one violation per given type, each with the criterion type the API delivers for it.
        /// </summary>
        public static List<ComplianceViolation> CreateTypedViolations(params ComplianceViolationType[] types)
        {
            return types.Select(type => new ComplianceViolation
            {
                Type = type,
                Criterion = new ComplianceCriterion
                {
                    CriterionType = type == ComplianceViolationType.NotAssessable
                        ? nameof(CriterionType.Assessability)
                        : nameof(CriterionType.Matrix)
                }
            }).ToList();
        }

        /// <summary>
        /// Builds an active accept rule, optionally carrying its current violation.
        /// </summary>
        public static Rule CreateActiveRule(string ruleUid, ComplianceViolation? currentViolation = null, int mgmtId = 1)
        {
            Rule rule = new()
            {
                Id = 1000 + mgmtId,
                Uid = ruleUid,
                MgmtId = mgmtId,
                Name = ruleUid,
                Action = "accept"
            };
            if (currentViolation != null)
            {
                rule.Violations.Add(currentViolation);
            }
            return rule;
        }

        /// <summary>
        /// Builds the rendered violation-details string the diff report is expected to produce.
        /// </summary>
        public static string CreateViolationDetailsControlString(DateTime foundDate, int violationId)
        {
            return $"Found: ({foundDate:dd.MM.yyyy} - {foundDate:HH:mm}) Test violation {violationId}";
        }
    }
}
