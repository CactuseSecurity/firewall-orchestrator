using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Data;
using FWO.Data.Extensions;
using FWO.Ui.Display;

namespace FWO.Compliance
{
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Checks whether a rule can be assessed, i.e. contains only evaluable network objects.
        /// </summary>
        public Task<bool> CheckAssessability(Rule rule, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations, ComplianceCriterion criterion)
        {
            bool isAssessable = true;

            resolvedSources = TryFilterDynamicAndDomainObjects(resolvedSources);
            resolvedDestinations = TryFilterDynamicAndDomainObjects(resolvedDestinations);

            if (rule.Action == "accept")
            {
                foreach (NetworkObject networkObject in resolvedSources.Concat(resolvedDestinations))
                {
                    AssessabilityIssue? assessabilityIssue = TryGetAssessabilityIssue(networkObject);
                    if (assessabilityIssue != null)
                    {
                        ComplianceCheckResult complianceCheckResult;

                        if (resolvedSources.Contains(networkObject))
                        {
                            complianceCheckResult = new(rule, ComplianceViolationType.NotAssessable)
                            {
                                Source = networkObject
                            };
                        }
                        else
                        {
                            complianceCheckResult = new(rule, ComplianceViolationType.NotAssessable)
                            {
                                Destination = networkObject
                            };
                        }

                        complianceCheckResult.AssessabilityIssue = assessabilityIssue;
                        complianceCheckResult.Criterion = criterion;

                        CreateViolation(ComplianceViolationType.NotAssessable, rule, complianceCheckResult);
                        isAssessable = false;
                    }
                }
            }

            return Task.FromResult(isAssessable);
        }

        /// <summary>
        /// Evaluates a rule against all configured compliance criteria.
        /// </summary>
        public async Task<bool> CheckRuleCompliance(Rule rule, IEnumerable<ComplianceCriterion> criteria)
        {
            bool ruleIsCompliant = true;

            if (rule.Action == "accept")
            {
                List<NetworkObject> resolvedSources = RuleDisplayBase
                    .GetResolvedNetworkLocations(rule.Froms)
                    .Select(from => from.Object)
                    .ToList();

                List<NetworkObject> resolvedDestinations = RuleDisplayBase
                    .GetResolvedNetworkLocations(rule.Tos)
                    .Select(to => to.Object)
                    .ToList();

                try
                {
                    foreach (var criterion in criteria)
                    {
                        switch (criterion.CriterionType)
                        {
                            case nameof(CriterionType.Assessability):
                                ruleIsCompliant &= CheckAssessability(rule, resolvedSources, resolvedDestinations, criterion).Result;
                                break;
                            case nameof(CriterionType.Matrix):
                                ruleIsCompliant &= await CheckMatrixCompliance(rule, criterion, resolvedSources, resolvedDestinations);
                                break;
                            case nameof(CriterionType.ForbiddenService):
                                ruleIsCompliant &= CheckForForbiddenService(rule, criterion);
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.TryWriteError("Compliance Check", e, true);
                }
            }

            return ruleIsCompliant;
        }

        /// <summary>
        /// Calculates compliance for all provided rules (or the rules from the last check) and stores violations.
        /// </summary>
        public async Task<List<Rule>> CalculateCompliance(List<Rule>? rulesToCheck = null)
        {
            List<Rule> rules = rulesToCheck ?? RulesInCheck ?? [];

            int nonCompliantRules = 0;
            int checkedRules = 0;

            Logger.TryWriteInfo("Compliance Check", $"Checking compliance for {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            if (Policy == null || Policy.Criteria == null)
            {
                Logger.TryWriteError("Compliance Check", "Checking compliance for rules not possible, because criteria could not be loaded.", true);
                return await Task.FromResult(rules);
            }

            if (Policy.Criteria.Count == 0)
            {
                Logger.TryWriteError("Compliance Check", "Checking compliance for rules not possible, because policy does not contain criteria.", true);
                return await Task.FromResult(rules);
            }

            List<ComplianceCriterion> criteria = Policy.Criteria.Select(c => c.Content).ToList();
            if (criteria.Count == 0)
            {
                Logger.TryWriteError("Compliance Check", "Checking compliance for rules not possible, because criteria were malformed.", true);
                return await Task.FromResult(rules);
            }

            Logger.TryWriteInfo("Compliance Check", $"Checking compliance for {Policy.Criteria.Count} criteria.", LocalSettings.ComplianceCheckVerbose);

            foreach (Rule rule in rules)
            {
                bool ruleIsCompliant = await CheckRuleCompliance(rule, criteria);
                if (!ruleIsCompliant)
                {
                    nonCompliantRules++;
                }

                checkedRules++;
            }

            Logger.TryWriteInfo("Compliance Check", $"Checked compliance for {checkedRules} rules and found {nonCompliantRules} non-compliant rules. Total violations: {_currentViolations.Count}.", LocalSettings.ComplianceCheckVerbose);
            return await Task.FromResult(rules);
        }

        private void CreateViolation(ComplianceViolationType violationType, Rule rule, ComplianceCheckResult complianceCheckResult, string? detailsOverride = null)
        {
            ComplianceViolation violation = new()
            {
                RuleId = (int)rule.Id,
                RuleUid = rule.Uid ?? "",
                MgmtUid = Managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "",
                PolicyId = Policy?.Id ?? 0,
                CriterionId = complianceCheckResult.Criterion!.Id
            };

            switch (violationType)
            {
                case ComplianceViolationType.MatrixViolation:
                    if (!string.IsNullOrEmpty(detailsOverride))
                    {
                        violation.Details = detailsOverride;
                    }
                    else if (complianceCheckResult.Source is NetworkObject s && complianceCheckResult.Destination is NetworkObject d)
                    {
                        string sourceString = GetNwObjectString(s);
                        string destinationString = GetNwObjectString(d);
                        violation.Details = $"{_userConfig.GetText("H5839")}: {sourceString} (Zone: {complianceCheckResult.SourceZone?.Name ?? ""}) -> {destinationString} (Zone: {complianceCheckResult.DestinationZone?.Name ?? ""})";
                    }

                    break;
                case ComplianceViolationType.ServiceViolation:
                    if (complianceCheckResult.Service is NetworkService svc)
                    {
                        violation.Details = $"{_userConfig.GetText("H5840")}: {svc.Name}";
                    }
                    else
                    {
                        throw new ArgumentNullException(paramName: "complianceCheckResult.Service", message: "The service argument must be non-null when creating a service violation.");
                    }

                    break;
                case ComplianceViolationType.NotAssessable:
                    if (complianceCheckResult.AssessabilityIssue != null)
                    {
                        string networkObject = "";

                        if (complianceCheckResult.Source != null)
                        {
                            networkObject = GetNwObjectString(complianceCheckResult.Source);
                        }
                        else if (complianceCheckResult.Destination != null)
                        {
                            networkObject = GetNwObjectString(complianceCheckResult.Destination);
                        }

                        string assessabilityIssueType = complianceCheckResult.AssessabilityIssue.Value.ToAssessabilityIssueString();
                        violation.Details = $"{_userConfig.GetText("H5841")}: {_userConfig.GetText(assessabilityIssueType)}({networkObject})";
                    }

                    break;
                default:
                    return;
            }

            _currentViolations.Add(violation);
        }

        private bool CheckForForbiddenService(Rule rule, ComplianceCriterion criterion)
        {
            bool ruleIsCompliant = true;

            List<string> restrictedServices = [.. criterion.Content.Split(',').Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))];

            if (restrictedServices.Count > 0)
            {
                foreach (var service in rule.Services.Where(s => restrictedServices.Contains(s.Content.Uid)))
                {
                    ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.ServiceViolation)
                    {
                        Criterion = criterion,
                        Service = service.Content
                    };

                    CreateViolation(ComplianceViolationType.ServiceViolation, rule, complianceCheckResult);
                    ruleIsCompliant = false;
                }
            }

            return ruleIsCompliant;
        }

        private string CreateUniqueViolationKey(ComplianceViolation violation)
        {
            string key = "";

            try
            {
                key = $"{violation.MgmtUid}_{violation.RuleUid}_{violation.PolicyId}_{violation.CriterionId}_{violation.Details}";
            }
            catch (Exception e)
            {
                Logger.TryWriteError("Compliance Check", e, true);
            }

            return key;
        }
    }
}
