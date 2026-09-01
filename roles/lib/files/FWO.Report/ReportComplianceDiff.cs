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
        private bool _existingViolationsFilterFailed;
        private HashSet<RuleIdentity> _suppressedRuleIdentities = [];
        private Dictionary<RuleIdentity, ComplianceViolationType> _currentComplianceBySuppressedRuleIdentity = [];

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
        /// Formats each violation with the time at which it first appeared in the report interval and marks retained
        /// non-impact rules with a localized message. A rule left without violations because the existing-violations
        /// filter suppressed them is labelled distinctly from a rule that genuinely had no violations in the interval
        /// when the current state remains non-compliant, with a separate label for a state that is not assessable.
        /// </summary>
        protected override void SetComplianceDataForRule(Rule rule, Func<ComplianceViolation, string>? formatter = null)
        {
            base.SetComplianceDataForRule(rule, FormatViolationDetails);

            if (ShowNonImpactRules && rule.Violations.Count == 0)
            {
                if (HasSuppressedIntervalViolations(rule))
                {
                    if (TryGetCurrentComplianceForSuppressedRule(rule, out ComplianceViolationType currentCompliance))
                    {
                        rule.Compliance = currentCompliance;
                        rule.ViolationDetails = currentCompliance == ComplianceViolationType.NotAssessable
                            ? userConfig.GetText("existing_violation_hidden_by_filter_not_assessable")
                            : userConfig.GetText("existing_violation_hidden_by_filter");
                    }
                    else
                    {
                        rule.ViolationDetails = userConfig.GetText("no_changes_found");
                    }
                }
                else
                {
                    rule.ViolationDetails = userConfig.GetText("no_changes_found");
                }
            }
        }

        /// <summary>
        /// Determines whether a rule was already non-compliant at reportStart and therefore had all its interval
        /// violations suppressed by the existing-violations filter, rather than genuinely having none.
        /// </summary>
        private bool HasSuppressedIntervalViolations(Rule rule)
        {
            string? managementUid = Managements.FirstOrDefault(management => management.Id == rule.MgmtId)?.Uid;
            return !string.IsNullOrEmpty(managementUid)
                && !string.IsNullOrEmpty(rule.Uid)
                && _suppressedRuleIdentities.Contains(new RuleIdentity(managementUid, rule.Uid));
        }

        /// <summary>
        /// Gets the current compliance state retained before interval violations were attached to a suppressed rule.
        /// </summary>
        private bool TryGetCurrentComplianceForSuppressedRule(Rule rule, out ComplianceViolationType currentCompliance)
        {
            string? managementUid = Managements.FirstOrDefault(management => management.Id == rule.MgmtId)?.Uid;
            if (string.IsNullOrEmpty(managementUid) || string.IsNullOrEmpty(rule.Uid))
            {
                currentCompliance = ComplianceViolationType.None;
                return false;
            }

            return _currentComplianceBySuppressedRuleIdentity.TryGetValue(
                new RuleIdentity(managementUid, rule.Uid),
                out currentCompliance);
        }

        /// <summary>
        /// Fetches diff data in the selective direction: violations first, then only the active rules referenced by
        /// those violations. This avoids paging through the entire active rule table when few rules changed.
        /// </summary>
        protected override async Task<List<Rule>[]?> FetchRuleChunks(int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct)
        {
            _existingViolationsFilterFailed = false;
            ReportData.ExistingViolationsFilterFailed = false;
            _suppressedRuleIdentities = [];
            _currentComplianceBySuppressedRuleIdentity = [];

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

            if (ShowNonImpactRules)
            {
                return await FetchAllActiveRules(intervalViolations, elementsPerFetch, apiConnection, ct);
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

            // The count is used only to schedule all offset pages concurrently; report semantics do not depend on it.
            AggregateCount? countResult = await apiConnection.SendQueryAsync<AggregateCount>(
                ComplianceQueries.countComplianceDiffViolations,
                countVariables);
            int violationCount = countResult?.Aggregate?.Count ?? 0;

            List<ComplianceViolation>[] chunks = await GetDataParallelized<ComplianceViolation>(
                violationCount,
                elementsPerFetch,
                apiConnection,
                ComplianceQueries.getComplianceDiffViolationsByChunk,
                (offset, limit) => CreateViolationPageVariables(offset, limit, violationsWhere),
                ct);

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
                    ComplianceQueries.getActiveViolationsBeforeDate,
                    (offset, limit) => CreatePreviousViolationVariables(candidateRuleUids, offset, limit, reportStart),
                    ct);

                HashSet<RuleIdentity> previouslyNonCompliantRules = previousViolationChunks
                    .SelectMany(chunk => chunk)
                    .Select(CreateRuleIdentity)
                    .ToHashSet();

                // Label only identities whose interval violations were actually suppressed. The historical set alone
                // can also contain identities from other managements that merely share a rule UID (see
                // CreatePreviousViolationVariables), which never had an interval violation to hide.
                _suppressedRuleIdentities = intervalViolations
                    .Select(CreateRuleIdentity)
                    .Where(previouslyNonCompliantRules.Contains)
                    .ToHashSet();

                // Remove all new violations for the rule, not just the first one that happened to match.
                return intervalViolations
                    .Where(violation => !previouslyNonCompliantRules.Contains(CreateRuleIdentity(violation)))
                    .ToList();
            }
            catch (Exception exception)
            {
                // Preserve the default diff rather than returning an empty report when only the optional filter fails.
                _existingViolationsFilterFailed = true;
                ReportData.ExistingViolationsFilterFailed = true;
                Log.TryWriteLog(LogType.Error, "Compliance Diff Report", $"Failed to fetch previous violations: {exception.Message}", DebugConfig.ExtendedLogReportGeneration);
                return intervalViolations;
            }
        }

        /// <summary>
        /// Marks archived reports when their requested existing-violation filter was unavailable.
        /// </summary>
        public override string SetDescription()
        {
            return _existingViolationsFilterFailed
                ? $"{base.SetDescription()} - {userConfig.GetText("existing_violations_filter_failed")}"
                : base.SetDescription();
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
                RuleQueries.getActiveRulesByUids,
                (offset, limit) => CreateRulePageVariables(candidateRuleUids, offset, limit),
                ct);

            AttachViolationsToRules(ruleChunks, violations);
            return ruleChunks;
        }

        /// <summary>
        /// Fetches every active rule through the standard compliance-report path when unchanged rules must be shown.
        /// The standard query includes current violations, so replace them with the already filtered interval violations
        /// to retain compliance-diff semantics.
        /// </summary>
        private async Task<List<Rule>[]?> FetchAllActiveRules(
            List<ComplianceViolation> violations,
            int elementsPerFetch,
            ApiConnection apiConnection,
            CancellationToken ct)
        {
            List<Rule>[]? ruleChunks = await base.FetchRuleChunks(elementsPerFetch, apiConnection, ct);
            if (ruleChunks == null)
            {
                return null;
            }

            AttachViolationsToRules(ruleChunks, violations, retainRulesWithoutViolations: true);
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
        /// Creates one offset page of the interval-violation query.
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
        /// Initial violations intentionally remain in this lookup because they establish that a rule was non-compliant
        /// at reportStart, even when initial violations are excluded from the displayed interval.
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
        private void AttachViolationsToRules(
            List<Rule>[] ruleChunks,
            List<ComplianceViolation> violations,
            bool retainRulesWithoutViolations = false)
        {
            Dictionary<RuleIdentity, List<ComplianceViolation>> violationsByRule = violations
                .GroupBy(CreateRuleIdentity)
                .ToDictionary(group => group.Key, group => group.OrderBy(violation => violation.FoundDate).ThenBy(violation => violation.Id).ToList());
            Dictionary<int, string> managementUidsById = Managements
                .ToDictionary(management => management.Id, management => management.Uid ?? "");

            foreach (List<Rule> ruleChunk in ruleChunks)
            {
                if (retainRulesWithoutViolations)
                {
                    foreach (Rule rule in ruleChunk)
                    {
                        PreserveCurrentComplianceForSuppressedRule(rule, managementUidsById);
                        TryAttachRuleViolations(rule, managementUidsById, violationsByRule);
                    }
                }
                else
                {
                    // The selective rule query uses separate UID and management lists. Retain only exact pairs that
                    // had violations, preventing cross-management UID matches from entering the report.
                    ruleChunk.RemoveAll(rule => !TryAttachRuleViolations(rule, managementUidsById, violationsByRule));
                }
            }
        }

        /// <summary>
        /// Stores the current compliance state before replacing active-rule violations with interval violations.
        /// </summary>
        private void PreserveCurrentComplianceForSuppressedRule(
            Rule rule,
            Dictionary<int, string> managementUidsById)
        {
            if (!managementUidsById.TryGetValue(rule.MgmtId, out string? managementUid)
                || string.IsNullOrEmpty(rule.Uid))
            {
                return;
            }

            RuleIdentity identity = new(managementUid, rule.Uid);
            if (_suppressedRuleIdentities.Contains(identity) && rule.Violations.Count > 0)
            {
                _currentComplianceBySuppressedRuleIdentity[identity] = DetermineCompliance(rule.Violations);
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
            // The all-active fallback query includes current violations. Always clear them before attaching only the
            // violations from this diff interval; unchanged rules must remain violation-free in the diff report.
            rule.Violations.Clear();

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
            return $"Found: ({violation.FoundDate:dd.MM.yyyy - HH:mm}) {violation.Details}";
        }
    }
}
