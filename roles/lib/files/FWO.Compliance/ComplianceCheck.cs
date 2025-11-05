using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Services;
using NetTools;
using FWO.Data.Report;
using FWO.Report.Filter.FilterTypes;
using FWO.Data.Middleware;
using System.Text.Json;
using FWO.Logging;
using FWO.Ui.Display;
using System.Linq.Expressions;
using FWO.Data.Extensions;

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        /// <summary>
        /// Active policy that defines the compliance criteria.
        /// </summary>
        public CompliancePolicy? Policy = null;
        /// <summary>
        /// Report object to create diff and to serve as dto.
        /// </summary>
        public ReportCompliance? ComplianceReport { get; set; } = null;
        /// <summary>
        /// Filters to create compliance reports.
        /// </summary>
        private ReportFilters _reportFilters = new();
        /// <summary>
        /// Network zones to use for matrix compliance check.
        /// </summary>
        public List<ComplianceNetworkZone> NetworkZones = [];

        private readonly ApiConnection _apiConnection;
        private readonly UserConfig _userConfig;

        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection)
        {
            _apiConnection = apiConnection;
            _userConfig = userConfig;

            if (_userConfig.GlobalConfig == null)
            {
                Log.WriteInfo("Compliance Check", "Global config not found.");
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
                Log.TryWriteLog(LogType.Info, "Compliance Check", "Starting compliance check.", LocalSettings.ComplianceCheckVerbose);

                if (_userConfig.GlobalConfig == null)
                {
                    Log.WriteInfo("Compliance Check", "Global config is necessary for compliance check, but was not found. Aborting compliance check.");
                    return;
                }

                if (_userConfig.GlobalConfig.ComplianceCheckPolicyId == 0)
                {
                    Log.WriteInfo("Compliance Check", "No Policy defined. Compliance check not possible.");
                    return;
                }

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Using policy {_userConfig.ComplianceCheckPolicyId}", LocalSettings.ComplianceCheckVerbose);

                Policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = _userConfig.GlobalConfig.ComplianceCheckPolicyId });

                if (Policy == null)
                {
                    Log.WriteError("Compliance Check", $"Policy with id {_userConfig.GlobalConfig.ComplianceCheckPolicyId} not found.");
                    return;
                }

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Policy criteria: {Policy.Criteria.Count} criteria found", LocalSettings.ComplianceCheckVerbose);

                if (Policy.Criteria.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Policy without criteria. Compliance check not possible.", LocalSettings.ComplianceCheckVerbose);
                    return;
                }
                
                foreach (var criterion in Policy.Criteria)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Criterion: {criterion.Content.Name} ({criterion.Content.CriterionType})", LocalSettings.ComplianceCheckVerbose);
                }

                Task loadNetworkZonesTask = LoadNetworkZones();
                Task setUpReportFiltersTask = SetUpReportFilters();

                await Task.WhenAll(loadNetworkZonesTask, setUpReportFiltersTask);

                ReportTemplate template = new("", _reportFilters.ToReportParams());

                ReportBase? currentReport = await ReportGenerator.GenerateFromTemplate(template, _apiConnection, _userConfig, DefaultInit.DoNothing);


                if (currentReport is ReportCompliance complianceReport)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Compliance report generated for {complianceReport.ReportData.ElementsCount} rules.", LocalSettings.ComplianceCheckVerbose);

                    ComplianceReport = complianceReport;

                    ComplianceReport.Violations.Clear();

                    await CheckRuleComplianceForAllRules();
                }
                else
                {
                    Log.WriteError("Compliance Check", "Could not generate compliance report.");
                }    
            }
            catch (System.Exception e)
            {
                Log.WriteError("Compliance Check", "Error while checking for compliance violations.", e);
            }
            
        }

        /// <summary>
        /// Updates the violation db table.
        /// </summary>
        public async Task PersistDataAsync()
        {
            try
            {
                Log.TryWriteLog(LogType.Info, "Compliance Check", "Persisting violations...", LocalSettings.ComplianceCheckVerbose);

                List<ComplianceViolation> violationsInDb = await _apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);

                // Filter violations by non-valuable rules. If rules are not evaluable their violation status stays datawise the same until they are evaluable again. 

                Task<List<int>> violationsForRemoveTask = GetViolationsForRemove(violationsInDb);

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {violationsInDb.Count} rows in violations db table.", LocalSettings.ComplianceCheckVerbose);

                List<ComplianceViolationBase> violations = await CreateViolationInsertObjects(violationsInDb);

                if (violations.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No new violations to persist.", LocalSettings.ComplianceCheckVerbose);
                }
                else
                {
                    object variablesAdd = new
                    {
                        violations
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variablesAdd);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Persisted {violations.Count} new violations.", LocalSettings.ComplianceCheckVerbose);
                }

                List<int> ids = await violationsForRemoveTask;

                if (ids.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No violations to remove.", LocalSettings.ComplianceCheckVerbose);
                }
                else
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"{ids.Count} violations to remove.", LocalSettings.ComplianceCheckVerbose);

                    DateTime removedAt = DateTime.UtcNow;

                    object variablesRemove = new
                    {
                        ids,
                        removedAt
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeViolations, variablesRemove);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Removed {ids.Count} violations.", LocalSettings.ComplianceCheckVerbose && ids.Count > 0);
                }
            }
            catch (Exception e)
            {
                Log.WriteError("Compliance Check", "Error while persisting compliance data.", e);
            }
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

        private async Task LoadNetworkZones()
        {
            if (Policy != null)
            {
                // ToDo later: work with several matrices?
                int? matrixId = Policy.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content.Id;
                if (matrixId != null)
                {
                    NetworkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
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

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {currentViolations.Count} current (i.e. removed_date == null) violations.", LocalSettings.ComplianceCheckVerbose);

                HashSet<string> violationKeys = currentViolations
                    .Select(ev => CreateUniqueViolationKey(ev))
                    .ToHashSet();

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Created {currentViolations.Count} unique keys for current violations.", LocalSettings.ComplianceCheckVerbose);

                violationsForInsert = complianceReport
                    .Violations
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

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Prepared {violationsForInsert.Count} new violations for insert.", LocalSettings.ComplianceCheckVerbose);
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
                Log.WriteError("Compliance Check", "Error creating unique violation key", Error: e);
            }

            return key;
        }

        private Task<List<int>> GetViolationsForRemove(List<ComplianceViolation> existingViolations)
        {
            List<int> violationsForUpdate = [];

            if (ComplianceReport is ReportCompliance complianceReport)
            {
                foreach (ComplianceViolation existingViolation in existingViolations.Where(ev => ev.RemovedDate == null).ToList())
                {
                    ComplianceViolation? validatedViolation = complianceReport.Violations.FirstOrDefault(v => CreateUniqueViolationKey(v) == CreateUniqueViolationKey(existingViolation));

                    if (validatedViolation == null)
                    {
                        violationsForUpdate.Add(existingViolation.Id);
                    }
                }
            }

            return Task.FromResult(violationsForUpdate);
        }
        
        private async Task CheckRuleComplianceForAllRules()
        {
            int nonCompliantRules = 0;

            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checking compliance for every rule.", LocalSettings.ComplianceCheckVerbose);

            foreach (Rule rule in ComplianceReport!.ReportData.RulesFlat)
            {
                bool ruleIsCompliant = await CheckRuleCompliance(rule);

                if (!ruleIsCompliant)
                {
                    nonCompliantRules++;
                }
            }

            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checked compliance for every rule and found {nonCompliantRules} non-compliant rules", LocalSettings.ComplianceCheckVerbose);
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
                            ruleIsCompliant &= await CheckAgainstMatrix(rule, criterion);
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

        private async Task<bool> CheckAgainstMatrix(Rule rule, ComplianceCriterion criterion)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges([.. rule.Froms.Select(nl => nl.Object)]);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges([.. rule.Tos.Select(nl => nl.Object)]);

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
                MgmtUid = ComplianceReport?.Managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "",
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

            ComplianceReport!.Violations.Add(violation);
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

        private async Task SetUpReportFilters()
        {
            Log.TryWriteLog(LogType.Info, "Compliance Check", "Setting up report filters for compliance check", LocalSettings.ComplianceCheckVerbose);

            _reportFilters = new()
            {
                ReportType = ReportType.ComplianceReport
            };

            _reportFilters.DeviceFilter.Managements = await _apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);

            foreach (var management in _reportFilters.DeviceFilter.Managements)
            {
                management.Selected = true;
                foreach (var device in management.Devices)
                {
                    device.Selected = true;
                }
            }
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

        private static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type == new NetworkObjectType() { Name = ObjectType.IPRange })
            {
                ranges.Add(IPAddressRange.Parse($"{networkObject.IP}-{networkObject.IpEnd}"));
            }
            else if (networkObject.Type != new NetworkObjectType() { Name = ObjectType.Group } && networkObject.ObjectGroupFlats.Length > 0)
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

        private List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> MapZonesToNetworkObjects(List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> inputData)
        {
            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> map = [];

            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) dataItem in inputData)
            {
                List<ComplianceNetworkZone>? networkZones = null;

                if (_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.AutoCalculateInternetZone && globalConfig.TreatDynamicAndDomainObjectsAsInternet && (dataItem.networkObject.Type.Name == "dynamic_net_obj" || dataItem.networkObject.Type.Name == "domain"))
                {
                    networkZones = NetworkZones.Where(zone => zone.IsAutoCalculatedInternetZone).ToList();
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

            // Get ip ranges that are not in any zone
            List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(x => x)];
            if (!(_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.AutoCalculateInternetZone) && undefinedIpRanges.Count > 0)
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


        private Expression<Func<bool>> CreateAssessabilityExpression(NetworkObject networkObject)
        {
            return () =>
                networkObject.IP == null && networkObject.IpEnd == null
                || (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "255.255.255.255/32")
                || (networkObject.IP == "::/128" && networkObject.IpEnd == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128")
                || (networkObject.IP == "255.255.255.255/32" && networkObject.IpEnd == "255.255.255.255/32")
                || (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "0.0.0.0/32");
        }
        
    }
}
