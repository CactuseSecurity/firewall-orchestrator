using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
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

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        public ReportCompliance? ComplianceReport { get; set; } = null;

        private ReportFilters _reportFilters = new();
        private CompliancePolicy? _policy = null;
        private List<ComplianceNetworkZone> _networkZones = [];

        private readonly UserConfig _userConfig;
        private readonly ApiConnection _apiConnection;
        private readonly DebugConfig _debugConfig;

        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection)
        {
            _userConfig = userConfig;
            _apiConnection = apiConnection;

            if (userConfig.GlobalConfig != null && !string.IsNullOrEmpty(userConfig.GlobalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(userConfig.GlobalConfig.DebugConfig) ?? new();
            }
            else
            {
                Log.WriteWarning("Compliance Check", "No debug config found, using default values.");

                _debugConfig = new();
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
                Log.TryWriteLog(LogType.Info, "Compliance Check", "Starting compliance check.", _debugConfig.ExtendedLogComplianceCheck);

                int? policyId = _userConfig.GlobalConfig?.ComplianceCheckPolicyId;

                if (policyId == null || policyId == 0)
                {
                    Log.WriteInfo("Compliance Check", "No Policy defined.");
                    return;
                }
                else
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Using policy {policyId}", _debugConfig.ExtendedLogComplianceCheck);
                }

                _policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = policyId });

                if (TryLogPolicyCriteria() == false)
                {
                    Log.WriteError("Compliance Check", $"Policy with id {policyId} not found.");
                    return;
                }

                Task loadNetworkZonesTask = LoadNetworkZones();
                Task setUpReportFiltersTask = SetUpReportFilters();

                await Task.WhenAll(loadNetworkZonesTask, setUpReportFiltersTask);

                ReportTemplate template = new("", _reportFilters.ToReportParams());

                ReportBase? currentReport = await ReportGenerator.GenerateFromTemplate(template, _apiConnection, _userConfig, DefaultInit.DoNothing);


                if (currentReport is ReportCompliance complianceReport)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Compliance report generated for {complianceReport.ReportData.ElementsCount} rules.", _debugConfig.ExtendedLogComplianceCheck);

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
                Log.TryWriteLog(LogType.Info, "Compliance Check", "Persisting violations...", _debugConfig.ExtendedLogComplianceCheck);

                List<ComplianceViolation> violationsInDb = await _apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);

                // Filter violations by non-valuable rules. If rules are not evaluable their violation status stays datawise the same until they are evaluable again. 

                Task<List<int>> violationsForRemoveTask = GetViolationsForRemove(violationsInDb);

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {violationsInDb.Count} rows in violations db table.", _debugConfig.ExtendedLogComplianceCheck);

                List<ComplianceViolationBase> violations = await CreateViolationInsertObjects(violationsInDb);

                if (violations.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No new violations to persist.", _debugConfig.ExtendedLogComplianceCheck);
                }
                else
                {
                    object variablesAdd = new
                    {
                        violations
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variablesAdd);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Persisted {violations.Count} new violations.", _debugConfig.ExtendedLogComplianceCheck);
                }

                List<int> ids = await violationsForRemoveTask;

                if (ids.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No violations to remove.", _debugConfig.ExtendedLogComplianceCheck);
                }
                else
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"{ids.Count} violations to remove.", _debugConfig.ExtendedLogComplianceCheck);

                    DateTime removedAt = DateTime.UtcNow;

                    object variablesRemove = new
                    {
                        ids,
                        removedAt
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeViolations, variablesRemove);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Removed {ids.Count} violations.", _debugConfig.ExtendedLogComplianceCheck && ids.Count > 0);
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
            _networkZones = networkZones;
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

        private bool TryLogPolicyCriteria()
        {
            if (_policy != null)
            {
                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Policy criteria: {_policy.Criteria.Count} criteria found", _debugConfig.ExtendedLogComplianceCheck);

                foreach (var criterion in _policy.Criteria)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Criterion: {criterion.Content.Name} ({criterion.Content.CriterionType})", _debugConfig.ExtendedLogComplianceCheck);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task LoadNetworkZones()
        {
            if (_policy != null)
            {
                // ToDo later: work with several matrices?
                int? matrixId = _policy.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content.Id;
                if (matrixId != null)
                {
                    _networkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
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

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {currentViolations.Count} current (i.e. removed_date == null) violations.", _debugConfig.ExtendedLogComplianceCheck);

                HashSet<string> violationKeys = currentViolations
                    .Select(ev => CreateUniqueViolationKey(ev))
                    .ToHashSet();

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Created {currentViolations.Count} unique keys for current violations.", _debugConfig.ExtendedLogComplianceCheck);

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

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Prepared {violationsForInsert.Count} new violations for insert.", _debugConfig.ExtendedLogComplianceCheck);
            }

            return Task.FromResult(violationsForInsert);
        }

        private string CreateUniqueViolationKey(ComplianceViolation violation)
        {
            string key = "";

            try
            {
                if (ComplianceReport is ReportCompliance complianceReport)
                {
                    key = $"{violation.MgmtUid}_{violation.RuleUid}_{violation.PolicyId}_{violation.CriterionId}_{violation.Details}";
                }
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

            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checking compliance for every rule.", _debugConfig.ExtendedLogComplianceCheck);

            foreach (Rule rule in ComplianceReport!.ReportData.RulesFlat)
            {
                bool ruleIsCompliant = await CheckRuleCompliance(rule);

                if (!ruleIsCompliant)
                {
                    nonCompliantRules++;
                }
            }

            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checked compliance for every rule and found {nonCompliantRules} non-compliant rules", _debugConfig.ExtendedLogComplianceCheck);
        }

        public async Task<bool> CheckRuleCompliance(Rule rule)
        {
            bool ruleIsCompliant = true;

            if (rule.Action == "accept")
            {
                foreach (var criterion in (_policy?.Criteria ?? []).Select(c => c.Content))
                {
                    switch (criterion.CriterionType)
                    {
                        case nameof(CriterionType.Matrix):
                            ruleIsCompliant &= await CheckAgainstMatrix(rule);
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

        private async Task<bool> CheckAgainstMatrix(Rule rule)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges([.. rule.Froms.Select(nl => nl.Object)]);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges([.. rule.Tos.Select(nl => nl.Object)]);

            await Task.WhenAll(fromsTask, tosTask);

            bool ruleIsCompliant = CheckMatrixCompliance(rule, fromsTask.Result, tosTask.Result);

            return ruleIsCompliant;
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

        private bool CheckMatrixCompliance(Rule rule, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> source, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> destination)
        {
            bool ruleIsCompliant = true;

            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> sourceZones = MapZonesToNetworkObjects(source);
            List<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> destinationZones = MapZonesToNetworkObjects(destination);
            ComplianceCriterion? matrixCriterion = _policy?.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content;

            if (!HandleUnresolvedZones(rule, sourceZones, true))
            {
                ruleIsCompliant = false;
            }

            if (!HandleUnresolvedZones(rule, destinationZones, false))
            {
                ruleIsCompliant = false;
            }

            foreach ((NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) sourceZone in sourceZones)
            {
                if (sourceZone.networkZones == null)
                {
                    continue;
                }

                foreach ((NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) destinationZone in destinationZones)
                {
                    if (destinationZone.networkZones == null)
                    {
                        continue;
                    }

                    if (!CheckZonePairCompliance(rule, matrixCriterion, sourceZone, destinationZone))
                    {
                        ruleIsCompliant = false;
                    }
                }
            }

            return ruleIsCompliant;
        }

        private bool HandleUnresolvedZones(Rule rule, IEnumerable<(NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones)> zones, bool isSource)
        {
            bool zonesResolvable = true;

            foreach ((NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) zone in zones)
            {
                if (zone.networkZones != null)
                {
                    continue;
                }

                CreateUnresolvableZoneViolation(rule, zone.networkObject, isSource);
                zonesResolvable = false;
            }

            return zonesResolvable;
        }

        private bool CheckZonePairCompliance(Rule rule, ComplianceCriterion? matrixCriterion, (NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) sourceZone, (NetworkObject networkObject, List<ComplianceNetworkZone>? networkZones) destinationZone)
        {
            bool zonesCompliant = true;

            foreach (ComplianceNetworkZone sourceNetworkZone in sourceZone.networkZones!)
            {
                foreach (ComplianceNetworkZone destinationNetworkZone in destinationZone.networkZones!)
                {
                    if (sourceNetworkZone.CommunicationAllowedTo(destinationNetworkZone))
                    {
                        continue;
                    }

                    AddMatrixViolation(rule, matrixCriterion, sourceZone.networkObject, sourceNetworkZone, destinationZone.networkObject, destinationNetworkZone);
                    zonesCompliant = false;
                }
            }

            return zonesCompliant;
        }

        private void AddMatrixViolation(Rule rule, ComplianceCriterion? matrixCriterion, NetworkObject source, ComplianceNetworkZone sourceZone, NetworkObject destination, ComplianceNetworkZone destinationZone)
        {
            ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.MatrixViolation)
            {
                Criterion = matrixCriterion,
                Source = source,
                SourceZone = sourceZone,
                Destination = destination,
                DestinationZone = destinationZone
            };

            ComplianceViolation? violation = TryCreateViolation(ComplianceViolationType.MatrixViolation, rule, complianceCheckResult);



            ComplianceReport!.Violations.Add(violation!);
        }

        private void CreateUnresolvableZoneViolation(Rule rule, NetworkObject networkObject, bool isSource)
        {
            ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.MatrixViolation)
            {
                Criterion = _policy?.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content,
            };

            if (isSource)
            {
                complianceCheckResult.Source = networkObject;
            }
            else
            {
                complianceCheckResult.Destination = networkObject;
            }

            ComplianceViolation? violation = TryCreateViolation(ComplianceViolationType.MatrixViolation, rule, complianceCheckResult);



            ComplianceReport!.Violations.Add(violation!);
        }

        private ComplianceViolation? TryCreateViolation(ComplianceViolationType violationType, Rule rule, ComplianceCheckResult complianceCheckResult)
        {
            ComplianceViolation violation = new()
            {
                RuleId = (int)rule.Id,
                RuleUid = rule.Uid ?? "",
                MgmtUid = ComplianceReport?.Managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "",
                PolicyId = _policy?.Id ?? 0
            };

            switch (violationType)
            {
                case ComplianceViolationType.MatrixViolation:

                    // Workaround!! TODO: Check compliance per criterion and transfer criterion id through the methods

                    violation.CriterionId = _policy?.Criteria
                                                .FirstOrDefault(criterionWrapper => criterionWrapper.Content.CriterionType == "Matrix")?
                                                .Content.Id ?? 0;

                    if (complianceCheckResult.Source is NetworkObject s && complianceCheckResult.Destination is NetworkObject d)
                    {
                        string sourceString = GetNwObjectString(s);
                        string destinationString = GetNwObjectString(d);
                        violation.Details = $"Matrix violation: {sourceString} (Zone: {complianceCheckResult.SourceZone?.Name ?? ""}) -> {destinationString} (Zone: {complianceCheckResult.DestinationZone?.Name ?? ""})";
                    }
                    else
                    {
                        if (complianceCheckResult.Source != null && complianceCheckResult.SourceZone == null)
                        {
                            violation.Details = $"Matrix violations: Failed to resolve zone for source {GetNwObjectString(complianceCheckResult.Source)}.";
                        }
                        else if (complianceCheckResult.Destination != null && complianceCheckResult.DestinationZone == null)
                        {
                            violation.Details = $"Matrix violations: Failed to resolve zone for destination {GetNwObjectString(complianceCheckResult.Destination)}.";
                        }
                        else
                        {
                            violation.Details = $"Matrix violation: Failed to resolve network objects.";
                        }
                    }

                    break;

                case ComplianceViolationType.ServiceViolation:

                    if (complianceCheckResult.Service is NetworkService svc)
                    {
                        violation.CriterionId = complianceCheckResult.Criterion?.Id ?? 0;
                        violation.Details = $"Restricted service used: {svc.Name}";
                    }
                    else
                    {
                        throw new ArgumentNullException(paramName: "complianceCheckResult.Service", message: "The service argument must be non-null when creating a service violation.");
                    }

                    break;

                default:

                    return null;
            }

            return violation;
        }

        private string GetNwObjectString(NetworkObject networkObject)
        {
            string networkObjectString = "";

            networkObjectString += networkObject.Name;
            networkObjectString += NwObjDisplay.DisplayIp(networkObject.IP, networkObject.IpEnd, networkObject.Type.Name, true);

            return networkObjectString;
        }

        private async Task SetUpReportFilters()
        {
            Log.TryWriteLog(LogType.Info, "Compliance Check", "Setting up report filters for compliance check", _debugConfig.ExtendedLogComplianceCheck);

            _reportFilters = new()
            {
                ReportType = ReportType.Compliance
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

                    ComplianceViolation? violation = TryCreateViolation(ComplianceViolationType.ServiceViolation, rule, complianceCheckResult);

                    if (violation != null)
                    {
                        ComplianceReport!.Violations.Add(violation);
                    }

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

                if (dataItem.ipRanges.Count > 0)
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

            foreach (ComplianceNetworkZone zone in _networkZones.Where(z => z.OverlapExists(ranges, unseenIpAddressRanges)))
            {
                result.Add(zone);
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
    }
}
