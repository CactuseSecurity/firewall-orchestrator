using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Interfaces;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using NetTools;
using FWO.Logging;
using FWO.Ui.Display;
using FWO.Data.Extensions;
using System.Net;
using System.Collections.Concurrent;
using FWO.Services;

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
    public class ComplianceCheck
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
        /// Wraps the stagtic class FWO.Logging.Log to make it accessible for unit tests.
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
        /// Parameter for treating domain and dynamic network objects during the check a part of the auto-calculated internet zone.
        /// </summary>
        private bool _treatDomainAndDynamicObjectsAsInternet = false;
        /// <summary>
        /// True if the feature auto-calculated internet zone is activated.
        /// </summary>
        private bool _autoCalculatedInternetZoneActive = false;
        /// <summary>
        /// Id of the compliance policy that is configurated for the check.
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
        /// Full compliance check to be called by scheduler.
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
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

                // Load data for evaluation.

                await LoadNetworkZones();

                // Perform check.

                RulesInCheck = await PerformCheckAsync(Managements!.Select(m => m.Id).ToList());

                if (RulesInCheck == null || RulesInCheck.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No relevant rules found. Compliance check not possible.", true);
                    return;
                }
                ;

                Logger.TryWriteInfo("Compliance Check", "Compliance check completed.", true);
            }
            catch (Exception e)
            {
                Logger.TryWriteError("Compliance Check", e, true);
            }

        }

        /// <summary>
        /// Retrieves rules with violations from db, calculate current violations, fills argument collections as diffs.
        /// </summary>
        /// <param name="managementIds"></param>
        /// <returns></returns>
        public async Task<List<Rule>> PerformCheckAsync(List<int> managementIds)
        {
            // Getting max import id for query vars.

            long? maxImportId = 0;


            Import? import = await _apiConnection.SendQueryAsync<Import>(ImportQueries.getMaxImportId);

            if (import != null && import.ImportAggregate != null && import.ImportAggregate.ImportAggregateMax != null)
            {
                maxImportId = import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? 0;

            }

            // Getting total number oc rules, for calculating chunks.

            AggregateCount? result = await _apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countActiveRules);
            int activeRulesCount = result?.Aggregate?.Count ?? 0;

            Logger.TryWriteInfo("Compliance Check", $"Loading {activeRulesCount} active rules in chunks of {_elementsPerFetch} for managements: {string.Join(",", managementIds)}.", LocalSettings.ComplianceCheckVerbose);

            // Retrieve rules and check current compliance for every rule.

            List<Rule>[]? chunks = await _parallelProcessor.SendParallelizedQueriesAsync<Rule>(activeRulesCount, _maxDegreeOfParallelism, _elementsPerFetch, RuleQueries.getRulesForSelectedManagements, CalculateCompliance, managementIds, maxImportId);

            if (chunks == null)
            {
                Logger.TryWriteInfo("Compliance Check", $"Chunks could not be loaded from the database.", LocalSettings.ComplianceCheckVerbose);
                return [];
            }

            Logger.TryWriteInfo("Compliance Check", $"Attempted to load {chunks.Length} chunks of rules.", LocalSettings.ComplianceCheckVerbose);

            List<Rule>? rules = chunks
                .SelectMany(rule => rule)
                .ToList();

            Logger.TryWriteInfo("Compliance Check", $"Loaded {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            CurrentViolationsInCheck = _currentViolations.ToList();

            Logger.TryWriteInfo("Compliance Check", $"Found {CurrentViolationsInCheck.Count} violations.", LocalSettings.ComplianceCheckVerbose);

            Logger.TryWriteInfo("Compliance Check", $"Post-processing {rules.Count} rules.", LocalSettings.ComplianceCheckVerbose);

            // Create diffs and fill argument bags.

            await PostProcessRulesAsync(rules);

            return rules;
        }

        public Task PostProcessRulesAsync(List<Rule> ruleFromDb)
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

            // Get remove args.

            Logger.TryWriteInfo("Compliance Check", $"Getting violations to remove.", LocalSettings.ComplianceCheckVerbose);

            _violationsToRemove.Clear();

            foreach ((ComplianceViolation violation, string key) in dbViolationsWithKeys)
            {
                if (!currentKeySet.Contains(key))
                {
                    _violationsToRemove.Add(violation);
                }
            }

            Logger.TryWriteInfo("Compliance Check", $"Got {_violationsToRemove.Count} violations to remove.", LocalSettings.ComplianceCheckVerbose);

            // Get insert args.

            Logger.TryWriteInfo("Compliance Check", $"Getting violations to insert.", LocalSettings.ComplianceCheckVerbose);

            _violationsToAdd.Clear();

            foreach ((ComplianceViolation violation, string key) in currentViolationsWithKeys)
            {
                if (!dbKeySet.Contains(key))
                {
                    ComplianceViolationBase violationBase = ComplianceViolationBase.CreateBase(violation);
                    _violationsToAdd.Add(violationBase);
                }
            }

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
                    List<ComplianceViolationBase> violations = _violationsToAdd.Cast<ComplianceViolationBase>().ToList(); ;
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

        public Task<bool> CheckAssessability(Rule rule, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations, ComplianceCriterion criterion)
        {
            bool isAssessable = true;

            // If treated as part of internet zone dynamic and domain objects are irrelevant for the assessability check.

            resolvedSources = TryFilterDynamicAndDomainObjects(resolvedSources);
            resolvedDestinations = TryFilterDynamicAndDomainObjects(resolvedDestinations);

            // Check only accept rules for assessability.

            if (rule.Action == "accept")
            {
                foreach (NetworkObject networkObject in resolvedSources.Concat(resolvedDestinations))
                {
                    // Get assessability issue type if existing.

                    AssessabilityIssue? assessabilityIssue = TryGetAssessabilityIssue(networkObject);

                    if (assessabilityIssue != null)
                    {
                        // Create check result object.

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

                        // Create violation.

                        CreateViolation(ComplianceViolationType.NotAssessable, rule, complianceCheckResult);
                        isAssessable = false;
                    }
                }
            }

            return Task.FromResult(isAssessable);
        }

        public async Task<bool> CheckRuleCompliance(Rule rule, IEnumerable<ComplianceCriterion> criteria)
        {
            bool ruleIsCompliant = true;

            if (rule.Action == "accept")
            {
                // Resolve network locations

                NetworkLocation[] networkLocations = rule.Froms.Concat(rule.Tos).ToArray();
                List<NetworkLocation> resolvedNetworkLocations = RuleDisplayBase.GetResolvedNetworkLocations(networkLocations);

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
                catch (System.Exception e)
                {
                    Logger.TryWriteError("Compliance Check", e, true);
                }

            }

            return ruleIsCompliant;
        }

        public static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type.Name == ObjectType.IPRange || (networkObject.Type.Name == ObjectType.Network && networkObject.IP.Equals(networkObject.IpEnd) == false))
            {
                if (IPAddress.TryParse(networkObject.IP.StripOffNetmask(), out IPAddress? ipStart) && IPAddress.TryParse(networkObject.IpEnd.StripOffNetmask(), out IPAddress? ipEnd))
                {
                    ranges.Add(new IPAddressRange(ipStart, ipEnd));
                }
            }
            else if (networkObject.Type.Name != ObjectType.Group && networkObject.ObjectGroupFlats.Length > 0)
            {
                for (int j = 0; j < networkObject.ObjectGroupFlats.Length; j++)
                {
                    if (networkObject.ObjectGroupFlats[j].Object != null)
                    {
                        ranges.AddRange(ParseIpRange(networkObject.ObjectGroupFlats[j].Object!));
                    }
                }
            }
            else if (networkObject.IP != null)
            {
                // CIDR notation or single (host) IP can be parsed directly
                ranges.Add(IPAddressRange.Parse(networkObject.IP));
            }

            return ranges;
        }

        /// <summary>
        /// Compliance check used in current UI implementation
        /// </summary>
        /// <param name="sourceIpRange"></param>
        /// <param name="destinationIpRange"></param>
        /// <param name="networkZones"></param>
        /// <returns></returns>
        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, List<ComplianceNetworkZone> networkZones)
        {
            NetworkZones = networkZones;
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = [];

            if (sourceIpRange != null && destinationIpRange != null)
            {
                CheckMatrixCompliance
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }

            return forbiddenCommunicationsOutput;
        }

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
                    Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while parsing relevant mangement IDs: {e.Message}", LocalSettings.ComplianceCheckVerbose);
                }
            }

            return filteredManagements;
        }

        private async Task<bool> CheckMatrixCompliance(Rule rule, ComplianceCriterion criterion, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges(resolvedSources);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges(resolvedDestinations);

            await Task.WhenAll(fromsTask, tosTask);

            bool ruleIsCompliant = true;

            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> sourceZones = MapZonesToNetworkObjects(fromsTask.Result);
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> destinationZones = MapZonesToNetworkObjects(tosTask.Result);

            foreach ((NetworkObject networkObject, List<ComplianceNetworkZone> networkZones) sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone sourceNetworkZone in sourceZone.networkZones)
                {
                    foreach ((NetworkObject networkObject, List<ComplianceNetworkZone> networkZones) destinationZone in destinationZones)
                    {
                        foreach (ComplianceNetworkZone destinationNetworkZone in destinationZone.networkZones)
                        {
                            if (!sourceNetworkZone.CommunicationAllowedTo(destinationNetworkZone))
                            {
                                ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.MatrixViolation)
                                {
                                    Criterion = criterion,
                                    Source = sourceZone.networkObject,
                                    SourceZone = sourceNetworkZone,
                                    Destination = destinationZone.networkObject,
                                    DestinationZone = destinationNetworkZone
                                };

                                CreateViolation(ComplianceViolationType.MatrixViolation, rule, complianceCheckResult);
                                ruleIsCompliant = false;
                            }
                        }
                    }
                }
            }

            return ruleIsCompliant;
        }

        private void CreateViolation(ComplianceViolationType violationType, Rule rule, ComplianceCheckResult complianceCheckResult)
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

                    if (complianceCheckResult.Source is NetworkObject s && complianceCheckResult.Destination is NetworkObject d)
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

        private string GetNwObjectString(NetworkObject networkObject)
        {
            string networkObjectString = "";

            networkObjectString += networkObject.Name;
            networkObjectString += NwObjDisplay.DisplayIp(networkObject.IP, networkObject.IpEnd, networkObject.Type.Name, true);

            return networkObjectString;
        }

        private bool CheckMatrixCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            // Determine all matching source zones
            List<ComplianceNetworkZone> sourceZones = DetermineZones(source);

            // Determine all macthing destination zones
            List<ComplianceNetworkZone> destinationZones = DetermineZones(destination);

            forbiddenCommunication = [];

            foreach (ComplianceNetworkZone sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone destinationZone in destinationZones.Where(d => !sourceZone.CommunicationAllowedTo(d)))
                {
                    forbiddenCommunication.Add((sourceZone, destinationZone));
                }
            }

            return forbiddenCommunication.Count == 0;
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

        private static Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> GetNetworkObjectsWithIpRanges(List<NetworkObject> networkObjects)
        {
            List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> networkObjectsWithIpRange = [];

            foreach (NetworkObject networkObject in networkObjects)
            {
                networkObjectsWithIpRange.Add((networkObject, ParseIpRange(networkObject)));
            }

            return Task.FromResult(networkObjectsWithIpRange);
        }


        private async Task LoadNetworkZones()
        {
            if (Policy != null)
            {
                // ToDo later: work with several matrices?
                int? matrixId = Policy.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content.Id;
                if (matrixId != null)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Loading network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                    NetworkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
                    Logger.TryWriteInfo("Compliance Check", $"Loaded {NetworkZones.Count} network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                }
            }
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

        private List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> MapZonesToNetworkObjects(List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> inputData)
        {
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> map = [];

            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) dataItem in inputData)
            {
                List<ComplianceNetworkZone> networkZones = [];

                if (_autoCalculatedInternetZoneActive && _treatDomainAndDynamicObjectsAsInternet && (dataItem.networkObject.Type.Name == "dynamic_net_obj" || dataItem.networkObject.Type.Name == "domain"))
                {
                    List<ComplianceNetworkZone> complianceNetworkZones = NetworkZones.Where(zone => zone.IsAutoCalculatedInternetZone).ToList();

                    foreach (ComplianceNetworkZone zone in complianceNetworkZones)
                    {
                        networkZones.Add(zone);
                    }
                }
                else if (dataItem.ipRanges.Count > 0)
                {
                    if (TryGetAssessabilityIssue(dataItem.networkObject) != null)
                    {
                        continue;
                    }

                    networkZones = DetermineZones(dataItem.ipRanges);
                }

                map.Add((dataItem.networkObject, networkZones));
            }

            return map;
        }

        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges)
        {
            List<ComplianceNetworkZone> result = [];
            List<List<IPAddressRange>> unseenIpAddressRanges = [];

            for (int i = 0; i < ranges.Count; i++)
            {
                unseenIpAddressRanges.Add(
                [
                    new(ranges[i].Begin, ranges[i].End)
                ]);
            }

            foreach (ComplianceNetworkZone zone in NetworkZones.Where(z => z.OverlapExists(ranges, unseenIpAddressRanges)))
            {
                result.Add(zone);
            }

            // No need to procceed if auto calculated internet zone is activated.

            if (_autoCalculatedInternetZoneActive)
            {
                return result;
            }

            // Get ip ranges that are not in any zone

            List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(x => x)];

            if (undefinedIpRanges.Count > 0)
            {
                result.Add
                (
                    new ComplianceNetworkZone()
                    {
                        Name = _userConfig.GetText("internet_local_zone"),
                    }
                );
            }

            return result;
        }

        private List<NetworkObject> TryFilterDynamicAndDomainObjects(List<NetworkObject> networkObjects)
        {
            if (_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.AutoCalculateInternetZone && globalConfig.TreatDynamicAndDomainObjectsAsInternet)
            {
                networkObjects = networkObjects
                    .Where(n => !new List<string> { "domain", "dynamic_net_obj" }.Contains(n.Type.Name))
                    .ToList();
            }

            return networkObjects;
        }

        private AssessabilityIssue? TryGetAssessabilityIssue(NetworkObject networkObject)
        {
            if (networkObject.IP == null && networkObject.IpEnd == null)
                return AssessabilityIssue.IPNull;

            if (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "255.255.255.255/32")
                return AssessabilityIssue.AllIPs;

            if (networkObject.IP == "::/128" && networkObject.IpEnd == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128")
                return AssessabilityIssue.AllIPs;

            if (networkObject.IP == "255.255.255.255/32" && networkObject.IpEnd == "255.255.255.255/32")
                return AssessabilityIssue.Broadcast;

            if (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "0.0.0.0/32")
                return AssessabilityIssue.HostAddress;

            return null;
        }

        #endregion
    }
}
