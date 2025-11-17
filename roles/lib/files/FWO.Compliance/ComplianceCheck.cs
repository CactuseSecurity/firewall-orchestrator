using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Interfaces;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Services;
using NetTools;
using FWO.Data.Report;
using FWO.Report.Filter.FilterTypes;
using FWO.Logging;
using FWO.Ui.Display;
using FWO.Data.Extensions;
using System.Net;

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        #region Props & fields

        /// <summary>
        /// Active policy that defines the compliance criteria.
        /// </summary>
        public CompliancePolicy? Policy = null;

        /// <summary>
        /// Report object to create diff and to serve as dto.
        /// </summary>
        public ReportCompliance? ComplianceReport { get; set; } = null;


        /// <summary>
        /// Network zones to use for matrix compliance check.
        /// </summary>
        public List<ComplianceNetworkZone> NetworkZones { get; set; } = [];

        public ILogger Logger { get; set; } = new Logger();

        /// <summary>
        /// Filters to create compliance reports.
        /// </summary>
        private ReportFilters _reportFilters = new();

        private readonly ApiConnection _apiConnection;
        private readonly UserConfig _userConfig;

        private List<Management>? _managements = [];
        private List<ComplianceViolation> _currentViolationsInCheck = [];

        private bool _treatDomainAndDynamicObjectsAsInternet = false;
        private bool _autoCalculatedInternetZoneActive = false;
        private int _complianceCheckPolicyId = 0;

        #endregion

        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection, ILogger? logger = null)
        {
            _apiConnection = apiConnection;
            _userConfig = userConfig;

            if (logger != null)
            {
                Logger = logger;
            }

            if (_userConfig.GlobalConfig == null)
            {
                Logger.TryWriteInfo("Compliance Check", "Global config not found.", _userConfig.GlobalConfig == null);
            }
        }

        /// <summary>
        /// Full compliance check to be called by scheduler
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
            try
            {
                Logger.TryWriteInfo("Compliance Check", "Starting compliance check.", LocalSettings.ComplianceCheckVerbose);

                GlobalConfig? globalConfig = _userConfig.GlobalConfig;

                if (globalConfig == null)
                {
                    Logger.TryWriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.", true);
                    return;
                }

                _complianceCheckPolicyId = globalConfig.ComplianceCheckPolicyId;
                _autoCalculatedInternetZoneActive = globalConfig.AutoCalculateInternetZone; 
                _treatDomainAndDynamicObjectsAsInternet = globalConfig.TreatDynamicAndDomainObjectsAsInternet;

                _managements  = await _apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

                if (_managements == null || _managements.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No managements found. Compliance check not possible.", true);
                    return;
                }

                if (_complianceCheckPolicyId == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No Policy defined. Compliance check not possible.", true);
                    return;
                }

                Logger.TryWriteInfo("Compliance Check", $"Using policy {_complianceCheckPolicyId}", LocalSettings.ComplianceCheckVerbose);

                Policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = _complianceCheckPolicyId });

                if (Policy == null)
                {
                    Logger.TryWriteError("Compliance Check", $"Policy with id {_complianceCheckPolicyId} not found.", true);
                    return;
                }

                Logger.TryWriteInfo("Compliance Check", $"Policy criteria: {Policy.Criteria.Count} criteria found", LocalSettings.ComplianceCheckVerbose);

                if (Policy.Criteria.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Policy without criteria. Compliance check not possible.", LocalSettings.ComplianceCheckVerbose);
                    return;
                }
                
                foreach (var criterion in Policy.Criteria)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Criterion: {criterion.Content.Name} ({criterion.Content.CriterionType})", LocalSettings.ComplianceCheckVerbose);
                }

                Task loadNetworkZonesTask = LoadNetworkZones();
                List<Rule> rules =  await GetRules();
                await Task.WhenAll(loadNetworkZonesTask);

                _currentViolationsInCheck.Clear();

                await CheckRuleComplianceForAllRules(rules);
                // List<Rule> rules = getRulesTask.Result;
                // Task setUpReportFiltersTask = SetUpReportFilters();

                // await Task.WhenAll(loadNetworkZonesTask, setUpReportFiltersTask);

                // ReportTemplate template = new("", _reportFilters.ToReportParams());

                // ReportBase? currentReport = await ReportGenerator.GenerateFromTemplate(template, _apiConnection, _userConfig, DefaultInit.DoNothing);


                // if (currentReport is ReportCompliance complianceReport)
                // {
                //     Logger.TryWriteInfo("Compliance Check", $"Compliance report generated for {complianceReport.ReportData.ElementsCount} rules.", LocalSettings.ComplianceCheckVerbose);

                //     ComplianceReport = complianceReport;

                //     ComplianceReport.Violations.Clear();

                //     await CheckRuleComplianceForAllRules();

                //     Logger.TryWriteInfo("Compliance Check", "Compliance check completed.", true);
                // }
                // else
                // {
                //     Logger.TryWriteError("Compliance Check", "Could not generate compliance report.", true);
                // }    
            }
            catch (System.Exception e)
            {
                Logger.TryWriteError("Compliance Check", e, true);
            }
            
        }

        /// <summary>
        /// Updates the violation db table.
        /// </summary>
        public async Task PersistDataAsync()
        {
            try
            {
                Logger.TryWriteInfo("Compliance Check", "Persisting violations...", LocalSettings.ComplianceCheckVerbose);

                List<ComplianceViolation> violationsInDb = await _apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);

                // Filter violations by non-valuable rules. If rules are not evaluable their violation status stays datawise the same until they are evaluable again. 

                Task<List<int>> violationsForRemoveTask = GetViolationsForRemove(violationsInDb);

                Logger.TryWriteInfo("Compliance Check", $"Found {violationsInDb.Count} rows in violations db table.", LocalSettings.ComplianceCheckVerbose);

                List<ComplianceViolationBase> violations = await CreateViolationInsertObjects(violationsInDb);

                if (violations.Count == 0)
                {
                    Logger.TryWriteInfo("Compliance Check", "No new violations to persist.", LocalSettings.ComplianceCheckVerbose);
                }
                else
                {
                    object variablesAdd = new
                    {
                        violations
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variablesAdd);

                    Logger.TryWriteInfo("Compliance Check", $"Persisted {violations.Count} new violations.", LocalSettings.ComplianceCheckVerbose);
                }

                List<int> ids = await violationsForRemoveTask;

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

        public async Task<bool> CheckRuleCompliance(Rule rule)
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
                
                foreach (var criterion in (Policy?.Criteria ?? []).Select(c => c.Content))
                {
                    switch (criterion.CriterionType)
                    {
                        case nameof(CriterionType.Assessability):
                            ruleIsCompliant &= CheckAssessability(rule, resolvedSources, resolvedDestinations, criterion).Result;
                            break;
                        case nameof(CriterionType.Matrix):
                            ruleIsCompliant &= await CheckAgainstMatrix(rule, criterion, resolvedSources, resolvedDestinations);
                            break;
                        case nameof(CriterionType.ForbiddenService):
                            ruleIsCompliant &= CheckForForbiddenService(rule, criterion);
                            break;
                        default:
                            break;
                    }
                }
            }

            return ruleIsCompliant;
        }

        public static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type.Name == ObjectType.IPRange )
            {
                if (IPAddress.TryParse(networkObject.IP.StripOffNetmask(), out IPAddress? ipStart) && IPAddress.TryParse(networkObject.IpEnd.StripOffNetmask(), out IPAddress? ipEnd))
                {
                    ranges.Add(new IPAddressRange(ipStart, ipEnd));
                }
                else
                {
                    
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
            else
            {
                if (networkObject.IP != null)
                {
                    // CIDR notation or single (host) IP can be parsed directly
                    ranges.Add(IPAddressRange.Parse(networkObject.IP));
                }
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

        private async Task<bool> CheckAgainstMatrix(Rule rule, ComplianceCriterion criterion, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges(resolvedSources);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges(resolvedDestinations);

            await Task.WhenAll(fromsTask, tosTask);

            bool ruleIsCompliant = CheckMatrixCompliance(rule, fromsTask.Result, tosTask.Result, criterion);

            return ruleIsCompliant;
        }

        private bool CheckMatrixCompliance(Rule rule, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> source, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> destination, ComplianceCriterion criterion)
        {
            bool ruleIsCompliant = true;

            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> sourceZones = MapZonesToNetworkObjects(source);
            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> destinationZones = MapZonesToNetworkObjects(destination);

            foreach ((NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone sourceNetworkZone in sourceZone.networkZones ?? [])
                {
                    foreach ((NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) destinationZone in destinationZones)
                    {
                        foreach (ComplianceNetworkZone destinationNetworkZone in destinationZone.networkZones ?? [])
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
                MgmtUid = _managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "",
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

            _currentViolationsInCheck.Add(violation);
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

        private async Task<List<Rule>> GetRules()
        {
            List<Rule> rules = await _apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForSelectedManagements);
            return rules;         
        }


        // private async Task SetUpReportFilters()
        // {
        //     Logger.TryWriteInfo("Compliance Check", "Setting up report filters for compliance check", LocalSettings.ComplianceCheckVerbose);

        //     _reportFilters = new()
        //     {
        //         ReportType = ReportType.ComplianceReport
        //     };

        //     _reportFilters.DeviceFilter.Managements = await _apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);

        //     foreach (var management in _reportFilters.DeviceFilter.Managements)
        //     {
        //         management.Selected = true;
        //         foreach (var device in management.Devices)
        //         {
        //             device.Selected = true;
        //         }
        //     }
        // }

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
                    NetworkZones =  await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
                }
            }
        }

        private Task<List<ComplianceViolationBase>> CreateViolationInsertObjects(List<ComplianceViolation> violationsInDb)
        {
            List<ComplianceViolationBase> violationsForInsert = [];

            if (ComplianceReport is ReportCompliance complianceReport)
            {
                List<ComplianceViolation> currentViolations = violationsInDb
                    .Where(ev => ev.RemovedDate == null)
                    .ToList();

                Logger.TryWriteInfo("Compliance Check", $"Found {currentViolations.Count} current (i.e. removed_date == null) violations.", LocalSettings.ComplianceCheckVerbose);

                HashSet<string> violationKeys = currentViolations
                    .Select(ev => CreateUniqueViolationKey(ev))
                    .ToHashSet();

                Logger.TryWriteInfo("Compliance Check", $"Created {currentViolations.Count} unique keys for current violations.", LocalSettings.ComplianceCheckVerbose);

                violationsForInsert = _currentViolationsInCheck
                    .Where(v => !violationKeys.Contains(CreateUniqueViolationKey(v)))
                    .Select(v => new ComplianceViolationBase
                    {
                        RuleId = v.RuleId,
                        RuleUid = v.RuleUid,
                        MgmtUid = v.MgmtUid,
                        Details = v.Details,
                        FoundDate = v.FoundDate,
                        RemovedDate = v.RemovedDate,
                        RiskScore = v.RiskScore,
                        PolicyId = v.PolicyId,
                        CriterionId = v.CriterionId
                    })
                    .ToList();

                Logger.TryWriteInfo("Compliance Check", $"Prepared {violationsForInsert.Count} new violations for insert.", LocalSettings.ComplianceCheckVerbose);
            }

            return Task.FromResult(violationsForInsert);
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

        private Task<List<int>> GetViolationsForRemove(List<ComplianceViolation> existingViolations)
        {
            List<int> violationsForUpdate = [];

            foreach (ComplianceViolation existingViolation in existingViolations.Where(ev => ev.RemovedDate == null).ToList())
            {
                ComplianceViolation? validatedViolation = _currentViolationsInCheck.FirstOrDefault(v => CreateUniqueViolationKey(v) == CreateUniqueViolationKey(existingViolation));

                if (validatedViolation == null)
                {
                    violationsForUpdate.Add(existingViolation.Id);
                }
            }

            return Task.FromResult(violationsForUpdate);
        }
        
        private async Task CheckRuleComplianceForAllRules(List<Rule>? rulesToCheck = null)
        {
            List<Rule> rules = rulesToCheck ?? ComplianceReport!.ReportData.RulesFlat;

            int nonCompliantRules = 0;

            Logger.TryWriteInfo("Compliance Check", $"Checking compliance for every rule.", LocalSettings.ComplianceCheckVerbose);

            foreach (Rule rule in rules)
            {
                bool ruleIsCompliant = await CheckRuleCompliance(rule);

                if (!ruleIsCompliant)
                {
                    nonCompliantRules++;
                }
            }

            Logger.TryWriteInfo("Compliance Check", $"Checked compliance for every rule and found {nonCompliantRules} non-compliant rules", LocalSettings.ComplianceCheckVerbose);
        }

        private List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> MapZonesToNetworkObjects(List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> inputData)
        {
            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> map = [];

            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) dataItem in inputData)
            {
                List<ComplianceNetworkZone>? networkZones = null;

                if (_autoCalculatedInternetZoneActive && _treatDomainAndDynamicObjectsAsInternet && (dataItem.networkObject.Type.Name == "dynamic_net_obj" || dataItem.networkObject.Type.Name == "domain"))
                {
                    List<ComplianceNetworkZone> complianceNetworkZones = NetworkZones.Where(zone => zone.IsAutoCalculatedInternetZone).ToList();

                    if (complianceNetworkZones.Count > 0)
                    {
                        networkZones = [];
                        foreach (ComplianceNetworkZone zone in complianceNetworkZones)
                        {
                            networkZones.Add(zone);
                        }
                    }
                }
                else if (dataItem.ipRanges.Count > 0)
                {
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
    }
}
