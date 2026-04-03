using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;

namespace FWO.Compliance
{
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Executes a compliance check based on the provided <see cref="ComplianceCheckType"/>.
        /// </summary>
        /// <param name="complianceCheckType"> Specifies the type of compliance check to perform.</param>
        public async Task RunComplianceCheck(ComplianceCheckType complianceCheckType)
        {
            switch (complianceCheckType)
            {
                case ComplianceCheckType.Variable:
                    bool isInitial = false;
                    AggregateCount violationCount = await _apiConnection.SendQueryAsync<AggregateCount>(ComplianceQueries.getViolationCount);
                    if (violationCount.Aggregate.Count == 0)
                    {
                        isInitial = true;
                    }

                    await CheckAll(isInitial);
                    break;
                case ComplianceCheckType.Standard:
                default:
                    await CheckAll();
                    break;
            }
        }

        /// <summary>
        /// Retrieves rules with violations from DB, calculates current violations, and prepares diff arguments.
        /// </summary>
        public async Task<List<Rule>> PerformCheckAsync(List<int> managementIds, bool isInitial = false)
        {
            long? maxImportId = 0;
            Import? import = await _apiConnection.SendQueryAsync<Import>(ImportQueries.getMaxImportId);

            if (import != null && import.ImportAggregate != null && import.ImportAggregate.ImportAggregateMax != null)
            {
                maxImportId = import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? 0;
            }

            AggregateCount? result = await _apiConnection.SendQueryAsync<AggregateCount>(
                RuleQueries.countActiveRules,
                new { mgm_ids = managementIds });
            int activeRulesCount = result?.Aggregate?.Count ?? 0;

            Logger.TryWriteInfo("Compliance Check", $"Loading {activeRulesCount} active rules in chunks of {_elementsPerFetch} for managements: {string.Join(",", managementIds)}.", LocalSettings.ComplianceCheckVerbose);

            _parallelProcessor.SetUp(activeRulesCount, _maxDegreeOfParallelism, _elementsPerFetch);
            List<Rule>[]? chunks = await _parallelProcessor.SendParallelizedQueriesAsync<Rule>(RuleQueries.getRulesForSelectedManagements, CalculateCompliance, managementIds, maxImportId);

            if (chunks == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Chunks could not be loaded from the database.", LocalSettings.ComplianceCheckVerbose);
                return [];
            }

            Logger.TryWriteInfo("Compliance Check", $"Attempted to load {chunks.Length} chunks of rules.", LocalSettings.ComplianceCheckVerbose);

            List<Rule>? rules = chunks.SelectMany(rule => rule).ToList();

            Logger.TryWriteInfo("Compliance Check", $"Loaded {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            CurrentViolationsInCheck = _currentViolations.ToList();

            Logger.TryWriteInfo("Compliance Check", $"Found {CurrentViolationsInCheck.Count} violations.", LocalSettings.ComplianceCheckVerbose);
            Logger.TryWriteInfo("Compliance Check", $"Post-processing {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            await PostProcessRulesAsync(rules, isInitial);
            return rules;
        }

        /// <summary>
        /// Creates insert/remove violation lists by comparing DB state with current check results.
        /// </summary>
        public Task PostProcessRulesAsync(List<Rule> ruleFromDb, bool isInitial = false)
        {
            List<(ComplianceViolation Violation, string Key)> dbViolationsWithKeys = ruleFromDb
                .SelectMany(rule => rule.Violations)
                .Select(violation => (violation, CreateUniqueViolationKey(violation)))
                .ToList();

            List<(ComplianceViolation Violation, string Key)> currentViolationsWithKeys = CurrentViolationsInCheck
                .Select(violation => (violation, CreateUniqueViolationKey(violation)))
                .ToList();

            HashSet<string> currentKeySet = currentViolationsWithKeys.Select(v => v.Key).ToHashSet(StringComparer.Ordinal);
            HashSet<string> dbKeySet = dbViolationsWithKeys.Select(v => v.Key).ToHashSet(StringComparer.Ordinal);

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Math.Max(1, _maxDegreeOfParallelism)
            };

            Logger.TryWriteInfo("Compliance Check", "Getting violations to remove.", LocalSettings.ComplianceCheckVerbose);
            _violationsToRemove.Clear();

            Parallel.ForEach(dbViolationsWithKeys, parallelOptions, pair =>
            {
                if (!currentKeySet.Contains(pair.Key))
                {
                    _violationsToRemove.Add(pair.Violation);
                }
            });

            Logger.TryWriteInfo("Compliance Check", $"Got {_violationsToRemove.Count} violations to remove.", LocalSettings.ComplianceCheckVerbose);
            Logger.TryWriteInfo("Compliance Check", "Getting violations to insert.", LocalSettings.ComplianceCheckVerbose);

            _violationsToAdd.Clear();

            Parallel.ForEach(currentViolationsWithKeys, parallelOptions, pair =>
            {
                if (!dbKeySet.Contains(pair.Key))
                {
                    ComplianceViolationBase violationBase = ComplianceViolationBase.CreateBase(pair.Violation, isInitial);
                    _violationsToAdd.Add(violationBase);
                }
            });

            Logger.TryWriteInfo("Compliance Check", $"Got {_violationsToAdd.Count} violations to insert.", LocalSettings.ComplianceCheckVerbose);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates the violation db table.
        /// </summary>
        public async Task PersistDataAsync()
        {
            try
            {
                Logger.TryWriteInfo("Compliance Check", "Persisting violations.", true);

                if (_violationsToAdd.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No new violations to persist.", LocalSettings.ComplianceCheckVerbose);
                }
                else
                {
                    List<ComplianceViolationBase> violations = _violationsToAdd.ToList();
                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, new { violations });
                    Logger.TryWriteInfo("Compliance Check", $"Persisted {_violationsToAdd.Count} new violations.", LocalSettings.ComplianceCheckVerbose);
                }

                List<int> ids = _violationsToRemove.Select(violation => violation.Id).ToList();
                if (ids.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No violations to remove.", LocalSettings.ComplianceCheckVerbose);
                }
                else
                {
                    Logger.TryWriteInfo("Compliance Check", $"{ids.Count} violations to remove.", LocalSettings.ComplianceCheckVerbose);
                    DateTime removedAt = DateTime.UtcNow;
                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeViolations, new { ids, removedAt });
                    Logger.TryWriteInfo("Compliance Check", $"Removed {ids.Count} violations.", LocalSettings.ComplianceCheckVerbose && ids.Count > 0);
                }

                Logger.TryWriteInfo("Compliance Check", "Persisting of violations completed.", true);
            }
            catch (Exception e)
            {
                Logger.TryWriteError("ComplianceCheck - PersistDataAsync", e, true);
            }
        }

        /// <summary>
        /// Filters the provided managements so that only the configured IDs remain.
        /// </summary>
        public static List<Management> GetRelevantManagements(GlobalConfig globalConfig, List<Management> managements)
        {
            List<Management>? filteredManagements = [];
            List<int> relevantManagementIDs = [];

            if (!string.IsNullOrEmpty(globalConfig.ComplianceCheckRelevantManagements))
            {
                try
                {
                    relevantManagementIDs = globalConfig.ComplianceCheckRelevantManagements
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();

                    filteredManagements = managements.Where(m => relevantManagementIDs.Contains(m.Id)).ToList();
                }
                catch (Exception e)
                {
                    Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while parsing relevant management IDs: {e.Message}", LocalSettings.ComplianceCheckVerbose);
                }
            }

            return filteredManagements;
        }

        /// <summary>
        /// Full compliance check
        /// </summary>
        private async Task CheckAll(bool isInitial = false)
        {
            DateTime startTime = DateTime.UtcNow;

            try
            {
                Logger.TryWriteInfo("Compliance Check", "Starting compliance check.", true);

                GlobalConfig? globalConfig = _userConfig.GlobalConfig;
                if (globalConfig == null)
                {
                    Logger.TryWriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.", true);
                    return;
                }

                _complianceCheckPolicyId = globalConfig.ComplianceCheckPolicyId;
                _autoCalculatedInternetZoneActive = globalConfig.AutoCalculateInternetZone;
                _treatDomainAndDynamicObjectsAsInternet = globalConfig.TreatDynamicAndDomainObjectsAsInternet;
                _elementsPerFetch = globalConfig.ComplianceCheckElementsPerFetch;
                _maxDegreeOfParallelism = globalConfig.ComplianceCheckAvailableProcessors;

                Logger.TryWriteInfo("Compliance Check", $"Parallelizing config: {_elementsPerFetch} elements per fetch and {_maxDegreeOfParallelism} processors.", LocalSettings.ComplianceCheckVerbose);

                if (_complianceCheckPolicyId == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No Policy defined. Compliance check not possible.", true);
                    return;
                }

                Policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = _complianceCheckPolicyId });
                if (Policy == null)
                {
                    Logger.TryWriteError("Compliance Check", $"Policy with id {_complianceCheckPolicyId} not found.", true);
                    return;
                }

                Managements = await _apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
                Managements = GetRelevantManagements(globalConfig, Managements);

                if (Managements == null || Managements.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No relevant managements found. Compliance check not possible.", true);
                    return;
                }

                Logger.TryWriteInfo("Compliance Check", $"Using policy {_complianceCheckPolicyId}", LocalSettings.ComplianceCheckVerbose);
                Logger.TryWriteInfo("Compliance Check", $"Policy criteria: {Policy.Criteria.Count} criteria found.", LocalSettings.ComplianceCheckVerbose);

                if (Policy.Criteria.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "Policy without criteria. Compliance check not possible.", LocalSettings.ComplianceCheckVerbose);
                    return;
                }

                foreach (var criterion in Policy.Criteria)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Criterion: {criterion.Content.Name} ({criterion.Content.CriterionType}).", LocalSettings.ComplianceCheckVerbose);
                }

                RulesInCheck = [];
                CurrentViolationsInCheck.Clear();
                _currentViolations.Clear();

                await LoadNetworkZones();

                RulesInCheck = await PerformCheckAsync(Managements.Select(m => m.Id).ToList(), isInitial);
                if (RulesInCheck == null || RulesInCheck.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No relevant rules found. Compliance check not possible.", true);
                    return;
                }

                TimeSpan elapsed = DateTime.UtcNow - startTime;
                Logger.TryWriteInfo("Compliance Check", $"Compliance check evaluated {RulesInCheck.Count} rules in {elapsed.TotalSeconds} seconds.", true);
                Logger.TryWriteInfo("Compliance Check", "Compliance check completed.", true);
            }
            catch (Exception e)
            {
                TimeSpan elapsed = DateTime.UtcNow - startTime;
                Logger.TryWriteInfo("Compliance Check", $"Compliance check failed after {elapsed.TotalSeconds} seconds.", true);
                Logger.TryWriteError("Compliance Check", e, true);
            }
        }
    }
}
