using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;

namespace FWO.Report
{
    public class ReportComplianceDiff : ReportCompliance
    {
        private readonly record struct RuleIdentity(string ManagementUid, string RuleUid);

        public int DiffReferenceInDays { get; set; } = 0;

        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {

        }

        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : base(query, userConfig, reportType, reportParams)
        {
            DiffReferenceInDays = reportParams.ComplianceFilter.DiffReferenceInDays;
        }

        /// <summary>
        /// A diff row is relevant only when its rule accepts traffic and is enabled.
        /// </summary>
        protected override bool ShowRule(Rule rule)
        {
            return base.ShowRule(rule) && !rule.Disabled;
        }

        /// <summary>
        /// Formats each violation with the time at which it first appeared in the report interval.
        /// </summary>
        protected override void SetComplianceDataForRule(Rule rule, Func<ComplianceViolation, string>? formatter = null)
        {
            base.SetComplianceDataForRule(rule, FormatViolationDetails);
        }

        /// <summary>
        /// Fetches diff data in the selective direction: violations first, then only the active rules referenced by
        /// those violations. This avoids paging through the entire active rule table when few rules changed.
        /// </summary>
        protected override async Task<List<Rule>[]?> FetchRuleChunks(int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct)
        {
            // Fix both boundaries once. Every parallel page therefore observes exactly the same report interval.
            DateTime reportEnd = DateTime.Now;
            DateTime reportStart = reportEnd.AddDays(-DiffReferenceInDays);

            List<ComplianceViolation> intervalViolations = await FetchIntervalViolations(
                reportStart,
                reportEnd,
                elementsPerFetch,
                apiConnection,
                ct);

            if (GlobalConfig.ComplianceDiffFilterExistingViolations)
            {
                intervalViolations = await RemoveViolationsForPreviouslyNonCompliantRules(
                    intervalViolations,
                    reportStart,
                    elementsPerFetch,
                    apiConnection,
                    ct);
            }

            return await FetchRulesForViolations(intervalViolations, elementsPerFetch, apiConnection, ct);
        }

        /// <summary>
        /// Reads all violations found inside the report interval. The database returns full violation details here so
        /// the later rule query does not need another nested violation relationship.
        /// </summary>
        private async Task<List<ComplianceViolation>> FetchIntervalViolations(
            DateTime reportStart,
            DateTime reportEnd,
            int elementsPerFetch,
            ApiConnection apiConnection,
            CancellationToken ct)
        {
            Dictionary<string, object> violationsWhere = CreateIntervalViolationsWhere(reportStart, reportEnd);
            Dictionary<string, object> countVariables = new()
            {
                ["where"] = violationsWhere
            };

            // we need the count to be able to parallelize the data fetch
            AggregateCount? countResult = await apiConnection.SendQueryAsync<AggregateCount>(
                ComplianceQueries.countComplianceDiffViolations,
                countVariables);
            int violationCount = countResult?.Aggregate?.Count ?? 0;

            List<ComplianceViolation>[] chunks = await GetDataParallelized<ComplianceViolation>(
                violationCount,
                elementsPerFetch,
                apiConnection,
                ct,
                ComplianceQueries.getComplianceDiffViolationsByChunk,
                (offset, limit) => CreateViolationPageVariables(offset, limit, violationsWhere));

            return chunks.SelectMany(chunk => chunk).ToList();
        }

        /// <summary>
        /// Removes every interval violation whose stable rule identity already had a violation active at reportStart.
        /// Historical lookups are batched by candidate rule UID, keeping the query proportional to changed rules.
        /// </summary>
        private async Task<List<ComplianceViolation>> RemoveViolationsForPreviouslyNonCompliantRules(
            List<ComplianceViolation> intervalViolations,
            DateTime reportStart,
            int elementsPerFetch,
            ApiConnection apiConnection,
            CancellationToken ct)
        {
            List<string> candidateRuleUids = GetDistinctRuleUids(intervalViolations);
            if (candidateRuleUids.Count == 0)
            {
                return [];
            }

            try
            {
                List<ComplianceViolation>[] previousViolationChunks = await GetDataParallelized<ComplianceViolation>(
                    candidateRuleUids.Count,
                    elementsPerFetch,
                    apiConnection,
                    ct,
                    ComplianceQueries.getActiveViolationsBeforeDate,
                    (offset, limit) => CreatePreviousViolationVariables(candidateRuleUids, offset, limit, reportStart));

                HashSet<RuleIdentity> previouslyNonCompliantRules = previousViolationChunks
                    .SelectMany(chunk => chunk)
                    .Select(CreateRuleIdentity)
                    .ToHashSet();

                // Remove all new violations for the rule, not just the first one that happened to match.
                return intervalViolations
                    .Where(violation => !previouslyNonCompliantRules.Contains(CreateRuleIdentity(violation)))
                    .ToList();
            }
            catch (Exception exception)
            {
                // Preserve the default diff rather than returning an empty report when only the optional filter fails.
                Log.TryWriteLog(LogType.Error, "Compliance Diff Report", $"Failed to fetch previous violations: {exception.Message}", DebugConfig.ExtendedLogReportGeneration);
                return intervalViolations;
            }
        }

        /// <summary>
        /// Fetches active rule details for the surviving violation identities and attaches the violations already read.
        /// Rules returned by the broad UID batches are matched again by management UID to avoid cross-management mixes.
        /// </summary>
        private async Task<List<Rule>[]> FetchRulesForViolations(
            List<ComplianceViolation> violations,
            int elementsPerFetch,
            ApiConnection apiConnection,
            CancellationToken ct)
        {
            List<string> candidateRuleUids = GetDistinctRuleUids(violations);
            if (candidateRuleUids.Count == 0)
            {
                return Array.Empty<List<Rule>>();
            }

            List<Rule>[] ruleChunks = await GetDataParallelized<Rule>(
                candidateRuleUids.Count,
                elementsPerFetch,
                apiConnection,
                ct,
                RuleQueries.getActiveRulesByUids,
                (offset, limit) => CreateRulePageVariables(candidateRuleUids, offset, limit));

            AttachViolationsToRules(ruleChunks, violations);
            return ruleChunks;
        }

        /// <summary>
        /// Builds the interval filter. Deliberately omitting removed_date retains violations that were found and then
        /// resolved within the selected window, matching the established diff-report behavior.
        /// </summary>
        private Dictionary<string, object> CreateIntervalViolationsWhere(DateTime reportStart, DateTime reportEnd)
        {
            List<string> managementUids = Managements
                .Select(management => management.Uid ?? "")
                .Where(uid => !string.IsNullOrEmpty(uid))
                .ToList();
            Dictionary<string, object> violationsWhere = new()
            {
                ["mgmt_uid"] = new Dictionary<string, object>
                {
                    ["_in"] = managementUids
                },
                ["found_date"] = new Dictionary<string, object>
                {
                    ["_gte"] = reportStart,
                    ["_lt"] = reportEnd
                }
            };

            if (GlobalConfig.ComplianceFilterOutInitialViolations)
            {
                violationsWhere["is_initial"] = new Dictionary<string, object>
                {
                    ["_eq"] = false
                };
            }

            return violationsWhere;
        }

        /// <summary>
        /// Creates one page of the interval-violation query.
        /// </summary>
        private static Dictionary<string, object> CreateViolationPageVariables(
            int offset,
            int limit,
            Dictionary<string, object> violationsWhere)
        {
            return new Dictionary<string, object>
            {
                ["offset"] = offset,
                ["limit"] = limit,
                ["where"] = violationsWhere
            };
        }

        /// <summary>
        /// Creates one historical lookup batch. The positive removal-date predicate selects violations that were still
        /// active at reportStart and avoids the expensive relationship-level NOT condition used by the old rule query.
        /// </summary>
        private Dictionary<string, object> CreatePreviousViolationVariables(
            List<string> candidateRuleUids,
            int offset,
            int limit,
            DateTime reportStart)
        {
            Dictionary<string, object> previousViolationsWhere = new()
            {
                ["rule_uid"] = new Dictionary<string, object>
                {
                    ["_in"] = candidateRuleUids.Skip(offset).Take(limit).ToList()
                },
                ["mgmt_uid"] = new Dictionary<string, object>
                {
                    ["_in"] = Managements.Select(management => management.Uid ?? "").ToList()
                },
                ["found_date"] = new Dictionary<string, object>
                {
                    ["_lt"] = reportStart
                },
                ["_or"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["removed_date"] = new Dictionary<string, object>
                        {
                            ["_is_null"] = true
                        }
                    },
                    new()
                    {
                        ["removed_date"] = new Dictionary<string, object>
                        {
                            ["_gte"] = reportStart
                        }
                    }
                }
            };

            return new Dictionary<string, object>
            {
                ["where"] = previousViolationsWhere
            };
        }

        /// <summary>
        /// Creates one active-rule lookup batch while reusing the standard rule-fragment variables.
        /// </summary>
        private Dictionary<string, object> CreateRulePageVariables(List<string> candidateRuleUids, int offset, int limit)
        {
            Dictionary<string, object> variables = CreateQueryVariables(0, 0, RuleQueries.getActiveRulesByUids);
            variables["rule_uids"] = candidateRuleUids.Skip(offset).Take(limit).ToList();
            return variables;
        }

        /// <summary>
        /// Associates interval violations with current rule versions by stable management/rule UIDs. A final exact-key
        /// check removes any extra rules produced by independently batching the two GraphQL IN lists.
        /// </summary>
        private void AttachViolationsToRules(List<Rule>[] ruleChunks, List<ComplianceViolation> violations)
        {
            Dictionary<RuleIdentity, List<ComplianceViolation>> violationsByRule = violations
                .GroupBy(CreateRuleIdentity)
                .ToDictionary(group => group.Key, group => group.OrderBy(violation => violation.FoundDate).ThenBy(violation => violation.Id).ToList());
            Dictionary<int, string> managementUidsById = Managements
                .ToDictionary(management => management.Id, management => management.Uid ?? "");

            foreach (List<Rule> ruleChunk in ruleChunks)
            {
                // The rule query uses separate UID and management lists. Retain only exact pairs that had violations.
                ruleChunk.RemoveAll(rule => !TryAttachRuleViolations(rule, managementUidsById, violationsByRule));
            }
        }

        /// <summary>
        /// Attaches violations to one rule and returns whether its exact identity was part of the diff.
        /// </summary>
        private static bool TryAttachRuleViolations(
            Rule rule,
            Dictionary<int, string> managementUidsById,
            Dictionary<RuleIdentity, List<ComplianceViolation>> violationsByRule)
        {
            if (!managementUidsById.TryGetValue(rule.MgmtId, out string? managementUid) || string.IsNullOrEmpty(rule.Uid))
            {
                return false;
            }

            RuleIdentity identity = new(managementUid, rule.Uid);
            if (!violationsByRule.TryGetValue(identity, out List<ComplianceViolation>? ruleViolations))
            {
                return false;
            }

            rule.Violations = ruleViolations;
            return true;
        }

        /// <summary>
        /// Returns sorted, unique rule UIDs for deterministic request batches and report ordering.
        /// </summary>
        private static List<string> GetDistinctRuleUids(List<ComplianceViolation> violations)
        {
            return violations
                .Select(violation => violation.RuleUid)
                .Where(ruleUid => !string.IsNullOrEmpty(ruleUid))
                .Distinct()
                .OrderBy(ruleUid => ruleUid)
                .ToList();
        }

        /// <summary>
        /// Builds the stable identity shared by historical and interval violations across rule imports.
        /// </summary>
        private static RuleIdentity CreateRuleIdentity(ComplianceViolation violation)
        {
            return new RuleIdentity(violation.MgmtUid, violation.RuleUid);
        }

        /// <summary>
        /// Formats the violation details shown in the diff report.
        /// </summary>
        private static string FormatViolationDetails(ComplianceViolation violation)
        {
            return $"Found: ({violation.FoundDate:dd.MM.yyyy - hh:mm}) {violation.Details}";
        }
    }
}
