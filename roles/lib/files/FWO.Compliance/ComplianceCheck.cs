using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Interfaces;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using System.Collections.Concurrent;
using FWO.Services;
using FWO.Services.Triviality;

namespace FWO.Compliance
{
    /// <summary>
    /// Provides the state and methods required to evaluate how well
    /// firewall management rules comply with the defined compliance policy.
    /// 
    /// The <c>ComplianceCheck</c> class encapsulates the logic used to analyze
    /// rule configurations, identify deviations from policy requirements,
    /// and deliver a structured assessment of compliance status.
    /// </summary>
    public partial class ComplianceCheck
    {
        #region Props & fields

        /// <summary>
        /// Active policy that defines the compliance criteria.
        /// </summary>
        public CompliancePolicy? Policy = null;

        /// <summary>
        /// Network zones to use for matrix compliance check.
        /// </summary>
        public List<ComplianceNetworkZone> NetworkZones { get; set; } = [];
        /// <summary>
        /// Network zones grouped by matrix criterion id for policies containing multiple matrices.
        /// </summary>
        private readonly Dictionary<int, List<ComplianceNetworkZone>> _networkZonesByCriterion = [];

        /// <summary>
        /// Wraps the static class FWO.Logging.Log to make it accessible for unit tests.
        /// </summary>
        public ILogger Logger { get; set; } = new Logger();

        /// <summary>
        /// Violations found in the last run of CheckAll.
        /// </summary>
        public List<ComplianceViolation> CurrentViolationsInCheck { get; private set; } = [];

        /// <summary>
        /// Rules that are to be evaluated in the next run of CheckAll.
        /// </summary>
        public List<Rule>? RulesInCheck { get; set; } = [];

        /// <summary>
        /// Managements that are the subjects of the check.
        /// </summary>
        public List<Management>? Managements { get; set; } = [];

        /// <summary>
        /// Access to API.
        /// </summary>
        private readonly ApiConnection _apiConnection;
        /// <summary>
        /// Access to user config.
        /// </summary>
        private readonly UserConfig _userConfig;

        /// <summary>
        /// Parameter for treating domain and dynamic network objects as part of the auto-calculated internet zone.
        /// </summary>
        private bool _treatDomainAndDynamicObjectsAsInternet = false;
        /// <summary>
        /// True if the feature auto-calculated internet zone is activated.
        /// </summary>
        private bool _autoCalculatedInternetZoneActive = false;
        /// <summary>
        /// Id of the compliance policy that is configured for the check.
        /// </summary>
        private int _complianceCheckPolicyId = 0;
        /// <summary>
        /// Number of elements that are treated as a chunk in parallelized processes
        /// </summary>
        private int _elementsPerFetch;
        /// <summary>
        /// Limit of threads that may be used for the compliance check.
        /// </summary>
        private int _maxDegreeOfParallelism;
        /// <summary>
        /// Collection that is suitable for parallel processing and receives and holds insert arguments for newly found violations.
        /// </summary>
        private readonly ConcurrentBag<ComplianceViolationBase> _violationsToAdd = new();
        /// <summary>
        /// Collection that is suitable for parallel processing and receives and holds remove arguments for deprecated violations.
        /// </summary>
        private readonly ConcurrentBag<ComplianceViolation> _violationsToRemove = new();
        /// <summary>
        /// Collection that is suitable for parallel processing and receives and holds violations as a result of the current check.
        /// </summary>
        private readonly ConcurrentBag<ComplianceViolation> _currentViolations = new();
        /// <summary>
        /// Multi-threading helper.
        /// </summary>
        private readonly ParallelProcessor _parallelProcessor;
        /// <summary>
        /// Key used to de-duplicate service candidates during forbidden-service evaluation.
        /// </summary>
        private readonly record struct ServiceMatchKey(
            long Id,
            string Uid,
            string Name,
            int? ProtocolId,
            int? DestinationPort,
            int? DestinationPortEnd)
        {
            public static ServiceMatchKey FromService(NetworkService service)
            {
                return new ServiceMatchKey(
                    service.Id,
                    service.Uid,
                    service.Name,
                    service.ProtoId ?? service.Protocol?.Id,
                    service.DestinationPort,
                    service.DestinationPortEnd);
            }
        }
        /// <summary>
        /// Evaluator for rule-level criteria that are attached to policies.
        /// </summary>
        private readonly RuleTrivialityEvaluator _ruleTrivialityEvaluator = new();

        #endregion

        #region Ctor

        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        /// <param name="logger">Log</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection, ILogger? logger = null)
        {
            _apiConnection = apiConnection;
            _userConfig = userConfig;

            if (logger != null)
            {
                Logger = logger;
            }

            _parallelProcessor = new(apiConnection, Logger);

            if (_userConfig.GlobalConfig == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Global config not found.", _userConfig.GlobalConfig == null);
            }

        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Executes a compliance check based on the provided <see cref="ComplianceCheckType"/>.
        /// </summary>
        /// <param name="complianceCheckType"> Specifies the type of compliance check to perform.</param>
        /// <remarks>
        /// When <paramref name="complianceCheckType"/> is <see cref="ComplianceCheckType.Variable"/>,
        /// the method first queries the system for existing violations.
        /// If no violations are found, the full compliance check is treated as an initial run.
        /// For <see cref="ComplianceCheckType.Standard"/> or other types,
        /// a standard full compliance check is performed without the initial flag.
        /// </remarks>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
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
        /// Evaluates the provided rules against all selected policies and returns true only if every selected policy passes.
        /// </summary>
        /// <param name="policyIds">Compliance policy identifiers to evaluate.</param>
        /// <param name="rulesToCheck">Rules to check for compliance.</param>
        public async Task<bool> AreRulesCompliant(IEnumerable<int> policyIds, IEnumerable<Rule> rulesToCheck)
        {
            GlobalConfig? globalConfig = _userConfig.GlobalConfig;
            if (globalConfig == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.", true);
                return false;
            }

            List<int> selectedPolicyIds = policyIds.Where(id => id > 0).Distinct().ToList();
            List<Rule> selectedRules = rulesToCheck.Select(rule => new Rule(rule)).ToList();

            if (selectedPolicyIds.Count == 0 || selectedRules.Count == 0)
            {
                return false;
            }

            ApplyGlobalConfig(globalConfig);
            Managements = await _apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

            foreach (int policyId in selectedPolicyIds)
            {
                Policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = policyId });
                if (Policy == null || Policy.Criteria.Count == 0)
                {
                    return false;
                }

                PrepareRuleComplianceState();
                await LoadNetworkZonesAsync();
                await CalculateCompliance(selectedRules.Select(rule => new Rule(rule)).ToList());
                CurrentViolationsInCheck = _currentViolations.ToList();

                if (CurrentViolationsInCheck.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluates the provided rules against a preloaded policy using request-scoped reference data.
        /// </summary>
        /// <param name="policy">Compliance policy to evaluate.</param>
        /// <param name="rulesToCheck">Rules to check for compliance.</param>
        /// <param name="managements">Managements valid for the current request.</param>
        /// <param name="networkZonesByCriterion">Network zones valid for the current request, keyed by matrix criterion id.</param>
        public async Task<bool> AreRulesCompliant(
            CompliancePolicy policy,
            IEnumerable<Rule> rulesToCheck,
            IEnumerable<Management> managements,
            IReadOnlyDictionary<int, List<ComplianceNetworkZone>> networkZonesByCriterion)
        {
            GlobalConfig? globalConfig = _userConfig.GlobalConfig;
            if (globalConfig == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.", true);
                return false;
            }

            List<Rule> selectedRules = rulesToCheck.Select(rule => new Rule(rule)).ToList();
            if (selectedRules.Count == 0 || policy.Criteria.Count == 0)
            {
                return false;
            }

            ApplyGlobalConfig(globalConfig);
            Managements = managements.ToList();
            Policy = policy;

            PrepareRuleComplianceState();
            LoadPreloadedNetworkZones(networkZonesByCriterion);
            await CalculateCompliance(selectedRules);
            CurrentViolationsInCheck = _currentViolations.ToList();

            return CurrentViolationsInCheck.Count == 0;
        }

        /// <summary>
        /// Retrieves rules with violations from DB, calculates current violations, and prepares diff arguments.
        /// </summary>
        /// <param name="managementIds">Management identifiers whose rules should be checked.</param>
        /// <param name="isInitial">Whether this is part of an initial check</param>
        /// <returns>List of all rules that have been analyzed.</returns>
        public async Task<List<Rule>> PerformCheckAsync(List<int> managementIds, bool isInitial = false)
        {
            // Getting max import id for query vars.

            long? maxImportId = 0;

            Import? import = await _apiConnection.SendQueryAsync<Import>(ImportQueries.getMaxImportId);

            if (import != null && import.ImportAggregate != null && import.ImportAggregate.ImportAggregateMax != null)
            {
                maxImportId = import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? 0;

            }

            // Getting total number of rules, for calculating chunks.

            AggregateCount? result = await _apiConnection.SendQueryAsync<AggregateCount>(
                RuleQueries.countActiveRules,
                new { mgm_ids = managementIds }
            );
            int activeRulesCount = result?.Aggregate?.Count ?? 0;

            Logger.TryWriteInfo("Compliance Check", $"Loading {activeRulesCount} active rules in chunks of {_elementsPerFetch} for managements: {string.Join(",", managementIds)}.", LocalSettings.ComplianceCheckVerbose);

            // Retrieve rules and check current compliance for every rule.

            _parallelProcessor.SetUp(activeRulesCount, _maxDegreeOfParallelism, _elementsPerFetch);

            bool requiresGlobalDuplicateIndex = Policy?.Criteria.Any(c => c.Content.CriterionType == nameof(CriterionType.ForbidBidirectionalDuplicate)) == true;
            Func<List<Rule>, Task<List<Rule>>>? postProcessAsync = requiresGlobalDuplicateIndex ? null : CalculateCompliance;

            List<Rule>[]? chunks = await _parallelProcessor.SendParallelizedQueriesAsync<Rule>(RuleQueries.getRulesForSelectedManagements, postProcessAsync, managementIds, maxImportId);

            if (chunks == null)
            {
                Logger.TryWriteInfo("Compliance Check", $"Chunks could not be loaded from the database.", LocalSettings.ComplianceCheckVerbose);
                return [];
            }

            Logger.TryWriteInfo("Compliance Check", $"Attempted to load {chunks.Length} chunks of rules.", LocalSettings.ComplianceCheckVerbose);

            List<Rule>? rules = chunks
                .SelectMany(rule => rule)
                .ToList();

            if (requiresGlobalDuplicateIndex)
            {
                await CalculateCompliance(rules);
            }

            Logger.TryWriteInfo("Compliance Check", $"Loaded {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            CurrentViolationsInCheck = _currentViolations.ToList();

            Logger.TryWriteInfo("Compliance Check", $"Found {CurrentViolationsInCheck.Count} violations.", LocalSettings.ComplianceCheckVerbose);

            Logger.TryWriteInfo("Compliance Check", $"Post-processing {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            // Create diffs and fill argument bags.

            await PostProcessRulesAsync(rules, isInitial);

            return rules;
        }

        /// <summary>
        /// Creates insert/remove violation lists by comparing DB state with current check results.
        /// </summary>
        /// <param name="ruleFromDb">Rules including the violations persisted in the database.</param>
        /// <param name="isInitial">Whether this is part of an initial check</param>
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

            // Get remove args.

            Logger.TryWriteInfo("Compliance Check", $"Getting violations to remove.", LocalSettings.ComplianceCheckVerbose);

            _violationsToRemove.Clear();

            Parallel.ForEach(
                dbViolationsWithKeys,
                parallelOptions,
                pair =>
                {
                    if (!currentKeySet.Contains(pair.Key))
                    {
                        _violationsToRemove.Add(pair.Violation);
                    }
                });

            Logger.TryWriteInfo("Compliance Check", $"Got {_violationsToRemove.Count} violations to remove.", LocalSettings.ComplianceCheckVerbose);

            // Get insert args.

            Logger.TryWriteInfo("Compliance Check", $"Getting violations to insert.", LocalSettings.ComplianceCheckVerbose);

            _violationsToAdd.Clear();

            Parallel.ForEach(
                currentViolationsWithKeys,
                parallelOptions,
                pair =>
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
                    object variablesAdd = new
                    {
                        violations
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variablesAdd);

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

                    object variablesRemove = new
                    {
                        ids,
                        removedAt
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeViolations, variablesRemove);

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
        /// <param name="globalConfig">Global configuration containing the ID list.</param>
        /// <param name="managements">All managements retrieved from the API.</param>
        /// <returns>Subset of managements that are relevant for the compliance check.</returns>
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
        #endregion

        #region Private Methods        

        /// <summary>
        /// Full compliance check
        /// </summary>
        /// <returns>Task that completes when the asynchronous compliance evaluation finished.</returns>
        private async Task CheckAll(bool isInitial = false)
        {
            DateTime startTime = DateTime.UtcNow;

            try
            {
                // Gathering necessary parameters for compliance check.

                Logger.TryWriteInfo("Compliance Check", "Starting compliance check.", true);

                GlobalConfig? globalConfig = _userConfig.GlobalConfig;

                if (globalConfig == null)
                {
                    Logger.TryWriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.", true);
                    return;
                }

                _complianceCheckPolicyId = globalConfig.ComplianceCheckPolicyId;
                ApplyGlobalConfig(globalConfig);

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
                    Logger.TryWriteInfo("Compliance Check", $"Policy without criteria. Compliance check not possible.", LocalSettings.ComplianceCheckVerbose);
                    return;
                }

                foreach (var criterion in Policy.Criteria)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Criterion: {criterion.Content.Name} ({criterion.Content.CriterionType}).", LocalSettings.ComplianceCheckVerbose);
                }

                // Clear previous check data

                RulesInCheck = [];
                CurrentViolationsInCheck.Clear();
                _currentViolations.Clear();
                _networkZonesByCriterion.Clear();

                // Load data for evaluation.

                await LoadNetworkZonesAsync();

                // Perform check.

                RulesInCheck = await PerformCheckAsync(Managements!.Select(m => m.Id).ToList(), isInitial);

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

        private void ApplyGlobalConfig(GlobalConfig globalConfig)
        {
            _autoCalculatedInternetZoneActive = globalConfig.AutoCalculateInternetZone;
            _treatDomainAndDynamicObjectsAsInternet = globalConfig.TreatDynamicAndDomainObjectsAsInternet;
            _elementsPerFetch = globalConfig.ComplianceCheckElementsPerFetch;
            _maxDegreeOfParallelism = globalConfig.ComplianceCheckAvailableProcessors;
        }

        /// <summary>
        /// Resets per-evaluation state before checking a policy.
        /// </summary>
        private void PrepareRuleComplianceState()
        {
            RulesInCheck = [];
            CurrentViolationsInCheck.Clear();
            _currentViolations.Clear();
            _networkZonesByCriterion.Clear();
        }

        /// <summary>
        /// Loads managements for the current compliance evaluation.
        /// </summary>
        private async Task LoadManagementsAsync()
        {
            Managements = await _apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
        }

        /// <summary>
        /// Builds a unique key identifying a violation over management, rule, policy, criterion, and detail.
        /// </summary>
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

        /// <summary>
        /// Calculates compliance for all provided rules (or the rules from the last check) and stores violations.
        /// </summary>
        /// <param name="rulesToCheck">Explicit set of rules; when null, the rules prepared by <see cref="CheckAll"/> are used.</param>
        /// <returns>List of rules that have been processed.</returns>
        public async Task<List<Rule>> CalculateCompliance(List<Rule>? rulesToCheck = null)
        {
            List<Rule> rules = rulesToCheck ?? RulesInCheck ?? [];

            int nonCompliantRules = 0;
            int checkedRules = 0;

            Logger.TryWriteInfo("Compliance Check", $"Checking compliance for {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            if (Policy == null || Policy.Criteria == null)
            {
                Logger.TryWriteError("Compliance Check", $"Checking compliance for rules not possible, because criteria could not be loaded.", true);
                return await Task.FromResult(rules);
            }

            if (Policy.Criteria.Count == 0)
            {
                Logger.TryWriteError("Compliance Check", $"Checking compliance for rules not possible, because policy does not contain criteria.", true);
                return await Task.FromResult(rules);
            }

            List<ComplianceCriterion> criteria = Policy.Criteria.Select(c => c.Content).ToList();

            if (criteria.Count == 0)
            {
                Logger.TryWriteError("Compliance Check", $"Checking compliance for rules not possible, because criteria were malformed.", true);
                return await Task.FromResult(rules);
            }

            Logger.TryWriteInfo("Compliance Check", $"Checking compliance for {Policy.Criteria.Count} criteria.", LocalSettings.ComplianceCheckVerbose);

            RuleBidirectionalDuplicateIndex? duplicateIndex = criteria.Any(c => c.CriterionType == nameof(CriterionType.ForbidBidirectionalDuplicate))
                ? new RuleBidirectionalDuplicateIndex(rules)
                : null;

            foreach (Rule rule in rules)
            {
                bool ruleIsCompliant = await CheckRuleCompliance(rule, criteria, duplicateIndex);

                if (!ruleIsCompliant)
                {
                    nonCompliantRules++;
                }

                checkedRules++;
            }

            Logger.TryWriteInfo("Compliance Check", $"Checked compliance for {checkedRules} rules and found {nonCompliantRules} non-compliant rules. Total violations: {_currentViolations.Count}.", LocalSettings.ComplianceCheckVerbose);
            return await Task.FromResult(rules);
        }

        #endregion
    }
}
