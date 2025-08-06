using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Encryption;
using FWO.Mail;
using FWO.Services;
using NetTools;
using Microsoft.AspNetCore.Http;
using FWO.Data.Report;
using FWO.Report.Filter.FilterTypes;
using FWO.Data.Middleware;
using System.Text.Json;
using FWO.Logging;

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        public ReportCompliance? ComplianceReport { get; set; } = null;
        
        public List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> Results { get; set; } = [];
        public List<ComplianceViolation> RestrictedServiceViolations { get; set; } = [];

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

            if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
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
            Log.TryWriteLog(LogType.Info, "Compliance Check", "Starting compliance check", _debugConfig.ExtendedLogComplianceCheck);

            int? policyId = _userConfig.GlobalConfig?.ComplianceCheckPolicyId;

            if (policyId == null || policyId == 0)
            {
                Log.WriteInfo("Compliance Check", "No Policy defined");
                return;
            }
            else
            {
                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Using policy {policyId}", _debugConfig.ExtendedLogComplianceCheck);
            }

            _policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = policyId });

            if (TryLogPolicyCriteria() == false)
            {
                Log.WriteError("Compliance Check", $"Policy with id {policyId} not found");
                return;
            }

            Task loadNetworkZonesTask = LoadNetworkZones();
            Task setUpReportFiltersTask = SetUpReportFilters();

            await Task.WhenAll(loadNetworkZonesTask, setUpReportFiltersTask);

            ReportTemplate template = new("", _reportFilters.ToReportParams());

            ReportBase? currentReport = await ReportGenerator.Generate(template, _apiConnection, _userConfig, DefaultInit.DoNothing);

            if (currentReport is ReportCompliance complianceReport)
            {
                ComplianceReport = complianceReport;
                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Compliance report generated with {complianceReport.ReportData.ManagementData.Count} managements", _debugConfig.ExtendedLogComplianceCheck);

                ComplianceReport = complianceReport;

                ComplianceReport.Violations.Clear();

                foreach (var management in complianceReport.ReportData.ManagementData)
                {
                    await CheckRuleCompliancePerManagement(management);
                }
                
            }
            else
            {
                Log.WriteError("Compliance Check", "Could not generate compliance report");
                return;
            }


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

        public async Task PersistDataAsync()
        {
            try
            {
                Log.TryWriteLog(LogType.Info, "Compliance Check", "Persisting violations...", _debugConfig.ExtendedLogComplianceCheck);

                List<ComplianceViolation> violationsInDb = await _apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {violationsInDb.Count} rows in violations db table.", _debugConfig.ExtendedLogComplianceCheck);

                List<ComplianceViolationBase> violations = await CreateViolationInsertObjectsAsync(violationsInDb);
                Task<List<int>> violationsForRemoveTask = GetViolationsForRemoveAsync(violationsInDb);


                if (violations.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No new violations to persist", _debugConfig.ExtendedLogComplianceCheck);
                }
                else
                {
                    object variablesAdd = new
                    {
                        violations
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variablesAdd);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Persisted {violations.Count} new violations", _debugConfig.ExtendedLogComplianceCheck);                    
                }

                if (violationsForRemoveTask.Result.Count == 0)
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", "No violations to remove.", _debugConfig.ExtendedLogComplianceCheck);
                }
                else
                {
                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"{violationsForRemoveTask.Result.Count} violations to remove.", _debugConfig.ExtendedLogComplianceCheck);

                    List<int> ids = violationsForRemoveTask.Result;
                    DateTime removedAt = DateTime.UtcNow;

                    object variablesRemove = new
                    {
                        ids,
                        removedAt
                    };

                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeViolations, variablesRemove);

                    Log.TryWriteLog(LogType.Info, "Compliance Check", $"Removed {violationsForRemoveTask.Result.Count} violations", _debugConfig.ExtendedLogComplianceCheck && violationsForRemoveTask.Result.Count > 0);                    
                }



            }
            catch (System.Exception e)
            {
                Log.WriteError("Compliance Check", "Error while persisting compliance data", e);
            }            
        }

        private async Task<List<ComplianceViolationBase>> CreateViolationInsertObjectsAsync(List<ComplianceViolation> violationsInDb)
        {
            List<ComplianceViolationBase> violationsForInsert = [];

            if (ComplianceReport is ReportCompliance complianceReport)
            {
                List<ComplianceViolation> currentViolations = violationsInDb
                    .Where(ev => ev.RemovedDate == null)
                    .ToList();

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Found {currentViolations.Count} current (i.e. removed_date == null) violations.", _debugConfig.ExtendedLogComplianceCheck);

                HashSet<string> violationKeys = currentViolations
                    .Select(ev => $"{ev.RuleId}_{ev.PolicyId}_{ev.CriterionId}_{ev.Details}")
                    .ToHashSet();

                Log.TryWriteLog(LogType.Info, "Compliance Check", $"Created {currentViolations.Count} unique keys for current violations.", _debugConfig.ExtendedLogComplianceCheck);

                violationsForInsert = complianceReport
                    .Violations
                    .Where(v => !violationKeys.Contains($"{v.RuleId}_{v.PolicyId}_{v.CriterionId}_{v.Details}"))
                    .Select(v => new ComplianceViolationBase
                    {
                        RuleId = v.RuleId,
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

            return violationsForInsert;
        }

        private async Task<List<int>> GetViolationsForRemoveAsync(List<ComplianceViolation> existingViolations)
        {
            List<int> violationsForUpdate = [];

            if (ComplianceReport is ReportCompliance complianceReport)
            {
                foreach (ComplianceViolation existingViolation in existingViolations.Where(ev => ev.RemovedDate == null).ToList())
                {
                    ComplianceViolation? validatedViolation = complianceReport.Violations.FirstOrDefault(v =>
                                                                v.RuleId == existingViolation.RuleId &&
                                                                v.PolicyId == existingViolation.PolicyId &&
                                                                v.CriterionId == existingViolation.CriterionId &&
                                                                v.Details == existingViolation.Details);
                                                                
                    if (validatedViolation == null)
                    {
                        if (existingViolation.Id is int id)
                        {
                            violationsForUpdate.Add(id);
                        }
                    }
                }
            }

            return violationsForUpdate;
        }




        private async Task CheckRuleCompliancePerManagement(ManagementReport management)
        {
            int notCompliantRules = 0;

            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checking compliance for management {management.Id} '{management.Name}'", _debugConfig.ExtendedLogComplianceCheck);

            foreach (var rulebase in management.Rulebases)
            {
                foreach (var rule in rulebase.Rules)
                {
                    rule.IsCompliant = await CheckRuleCompliance(rule);

                    if (!rule.IsCompliant)
                    {
                        notCompliantRules++;
                    }
                }
            }
            
            Log.TryWriteLog(LogType.Info, "Compliance Check", $"Checked compliance for management {management.Id} '{management.Name}' and found {notCompliantRules} non-compliant rules", _debugConfig.ExtendedLogComplianceCheck);
        }

        private async Task GatherCheckResults()
        {
            
            if (ComplianceReport is ReportCompliance complianceReport)
            {
                complianceReport.Violations.Clear();

                foreach (var item in Results)
                {
                    ComplianceViolation violation = new()
                    {
                        RuleId = (int)item.Item1.Id,
                        Details = $"Matrix violation: {item.Item2.Item1.Name} -> {item.Item2.Item2.Name}",
                        CriterionId = _policy?.Criteria
                                                .FirstOrDefault(criterionWrapper => criterionWrapper.Content.CriterionType == "Matrix")?
                                                .Content.Id ?? 0,
                        PolicyId = _policy?.Id ?? 0
                    };

                    complianceReport.Violations.Add(violation);
                }
                
                if (RestrictedServiceViolations.Count > 0)
                {
                   complianceReport.Violations.AddRange(RestrictedServiceViolations); 
                }
                
                await complianceReport.SetComplianceData();
            }
        }

        public async Task<bool> CheckRuleCompliance(Rule rule)
        {
            bool ruleIsCompliant = true;

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

            return ruleIsCompliant;
        }

        // private async Task<bool> CheckAgainstMatrix(Rule rule)
        // {
        //     Task<List<IPAddressRange>> fromsTask = GetIpRangesFromNetworkObjects([.. rule.Froms.Select(nl => nl.Object)]);
        //     Task<List<IPAddressRange>> tosTask = GetIpRangesFromNetworkObjects([.. rule.Tos.Select(nl => nl.Object)]);

        //     await Task.WhenAll(fromsTask, tosTask);

        //     List<IPAddressRange> froms = fromsTask.Result;
        //     List<IPAddressRange> tos = tosTask.Result;

        //     bool ruleIsCompliant = CheckMatrixCompliance(froms, tos, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication);

        //     foreach ((ComplianceNetworkZone, ComplianceNetworkZone) item in forbiddenCommunication)
        //     {
        //         Results.Add((rule, item));
        //     }
        //     return ruleIsCompliant;
        // }

        private async Task<bool> CheckAgainstMatrix(Rule rule)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRange([.. rule.Froms.Select(nl => nl.Object)]);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRange([.. rule.Tos.Select(nl => nl.Object)]);

            await Task.WhenAll(fromsTask, tosTask);

            bool ruleIsCompliant = CheckMatrixCompliance(rule, fromsTask.Result, tosTask.Result);

            return ruleIsCompliant;
        }

        private static Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> GetNetworkObjectsWithIpRange(List<NetworkObject> networkObjects)
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

        private bool CheckMatrixCompliance(Rule rule, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> source, List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> destination)
        {
            bool ruleIsCompliant = true;

            // Determine all matching source zones
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> sourceZones = [];
            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) sourceObject in source)
            {
                sourceZones.Add((sourceObject.networkObject, DetermineZones(sourceObject.ipRanges)));
            }
            
            // Determine all matching destination zones
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> destinationZones = [];
            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) destinationObject in destination)
            {
                destinationZones.Add((destinationObject.networkObject, DetermineZones(destinationObject.ipRanges)));
            }

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
                                ComplianceViolation violation = new()
                                {
                                    RuleId = (int)rule.Id,
                                    Details = $"Matrix violation: {sourceZone.networkObject.Name} (Uid: {sourceZone.networkObject.Uid} - Zone: {sourceNetworkZone.Id} - {sourceNetworkZone.Name}) ---> {destinationZone.networkObject.Name} (Uid: {destinationZone.networkObject.Uid} - Zone: {destinationNetworkZone.Id} - {destinationNetworkZone.Name})",
                                    CriterionId = _policy?.Criteria
                                                            .FirstOrDefault(criterionWrapper => criterionWrapper.Content.CriterionType == "Matrix")?
                                                            .Content.Id ?? 0,
                                    PolicyId = _policy?.Id ?? 0
                                };

                                ComplianceReport.Violations.Add(violation);

                                ruleIsCompliant = false;
                            }
                        }
                    }                    
                }
            }

            return ruleIsCompliant;
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

        // public List<ComplianceViolation> TryGetRestrictedServiceViolation(Rule rule, ComplianceCriterion criterion)
        // {
        //     List<ComplianceViolation> violations = [];
        //     List<string> restrictedServices = [.. criterion.Content.Split(',').Select(s => s.Trim())
        //             .Where(s => !string.IsNullOrEmpty(s))];

        //     if (restrictedServices.Count > 0)
        //     {
        //         foreach (var service in rule.Services.Where(s => restrictedServices.Contains(s.Content.Uid)))
        //         {
        //             ComplianceViolation violation = new()
        //             {
        //                 RuleId = (int)rule.Id,
        //                 Details = $"Restricted service used: {service.Content.Name}",
        //                 CriterionId = criterion.Id,
        //                 PolicyId = _policy?.Id ?? 0
        //             };
        //             violations.Add(violation);
        //         }
        //     }

        //     return violations;
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
                    ComplianceViolation violation = new()
                    {
                        RuleId = (int)rule.Id,
                        Details = $"Restricted service used: {service.Content.Name}",
                        CriterionId = criterion.Id,
                        PolicyId = _policy?.Id ?? 0
                    };
                    
                    ComplianceReport.Violations.Add(violation);
                }
            }

            return ruleIsCompliant;
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
                CheckMatrixComplianceOld
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }
            return forbiddenCommunicationsOutput;
        }

        private bool CheckMatrixComplianceOld(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
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
                        Name =  _userConfig.GetText("internet_local_zone"),
                    }
                );
            }

            return result;
        }
    }
}
