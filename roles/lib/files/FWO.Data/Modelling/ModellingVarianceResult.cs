using System;
using System.Collections.Generic;
using System.Linq;
using FWO.Data.Report;

namespace FWO.Data.Modelling
{
    public class ModProdDifference
    {
        public ModellingConnection ModelledConnection { get; set; } = new();
        public List<Rule> ImplementedRules { get; set; } = [];
    }

    public class AppRoleStats
    {
        public int ModelledAppRolesCount { get; set; } = 0;
        public int AppRolesOk { get; set; } = 0;
        public int AppRolesMissingCount { get; set; } = 0;
        public int AppRolesDifferenceCount { get; set; } = 0;
    }

    public class ModellingVarianceResult
    {
        public List<ModellingConnection> ConnsNotImplemented { get; set; } = [];
        public List<ModProdDifference> RuleDifferences { get; set; } = [];
        public List<ModProdDifference> OkRules { get; set; } = [];
        public Dictionary<int, List<Rule>> UnModelledRules { get; set; } = [];
        public Dictionary<int, List<Rule>> DeletedModelsRules { get; set; } = [];
        public int ModelledCount { get; set; } = 0;
        public List<Management> Managements { get; set; } = [];

        public Dictionary<int, List<ModellingAppRole>> MissingAppRoles { get; set; } = [];
        public Dictionary<int, List<ModellingAppRole>> DifferingAppRoles { get; set; } = [];
        public AppRoleStats AppRoleStats { get; set; } = new();

        public List<ManagementReport> UnModelledRulesReport { get; set; } = [];

        public void AddDifference(ModellingConnection conn, Rule rule)
        {
            ModProdDifference? diff = RuleDifferences.FirstOrDefault(d => d.ModelledConnection.Id == conn.Id);
            if (diff == null)
            {
                RuleDifferences.Add(new() { ModelledConnection = conn, ImplementedRules = [rule] });
            }
            else
            {
                diff.ImplementedRules.Add(rule);
            }
        }

        public void AddOkRule(ModellingConnection conn, Rule rule)
        {
            ModProdDifference? diff = OkRules.FirstOrDefault(d => d.ModelledConnection.Id == conn.Id);
            if (diff == null)
            {
                OkRules.Add(new() { ModelledConnection = conn, ImplementedRules = [rule] });
            }
            else
            {
                diff.ImplementedRules.Add(rule);
            }
        }

        public List<Rule> GetAllOkRules()
        {
            List<Rule> allOkRules = [];
            foreach (var rulesPerConn in OkRules.Select(x => x.ImplementedRules))
            {
                allOkRules.AddRange(rulesPerConn);
            }
            return allOkRules;
        }

        public List<ManagementReport> UnmodelledRuleDataToReport()
        {
            return MgtDataToReport(UnModelledRules);
        }

        public List<ManagementReport> DeletedConnRuleDataToReport()
        {
            return MgtDataToReport(DeletedModelsRules);
        }

        private List<ManagementReport> MgtDataToReport(Dictionary<int, List<Rule>> rulesToReport)
        {
            List<ManagementReport> managementReports = [];

            foreach ((int managementId, List<Rule> rulesPerManagement) in rulesToReport.Where(entry => entry.Value.Count > 0))
            {
                Management? management = Managements.FirstOrDefault(m => m.Id == managementId);
                Dictionary<int, Device> deviceMap = management?.Devices?.ToDictionary(d => d.Id) ?? [];
                Dictionary<string, Device> deviceNameMap = management?.Devices?
                                                                    .Where(d => !string.IsNullOrEmpty(d.Name))
                                                                    .GroupBy(d => d.Name!, StringComparer.OrdinalIgnoreCase)
                                                                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase) ?? [];

                ManagementReport managementReport = new()
                {
                    Id = managementId,
                    Name = management?.Name ?? "",
                    Uid = management?.Uid ?? ""
                };

                Dictionary<int, RulebaseReport> rulebaseReports = [];
                Dictionary<int, List<Rule>> rulebaseRules = [];

                Dictionary<int, DeviceAggregation> deviceAggregations = [];
                Dictionary<long, int> pseudoRulebaseIds = [];
                int nextPseudoRulebaseId = -1;

                foreach (Rule rule in rulesPerManagement)
                {
                    int deviceId = ResolveDeviceId(rule, management, deviceNameMap);
                    int rulebaseId = ResolveRulebaseId(rule, pseudoRulebaseIds, ref nextPseudoRulebaseId);

                    RulebaseReport rulebaseReport = GetOrCreateRulebaseReport(rulebaseReports, rulebaseId, rule);
                    if (!rulebaseRules.TryGetValue(rulebaseId, out List<Rule>? rulesForRulebase))
                    {
                        rulesForRulebase = [];
                        rulebaseRules.Add(rulebaseId, rulesForRulebase);
                    }

                    rulesForRulebase.Add(rule);

                    DeviceAggregation deviceAggregation = GetOrCreateDeviceAggregation(deviceAggregations, deviceId, rule, management, deviceMap, deviceNameMap);

                    EnsureRulebaseLink(deviceAggregation, deviceId, rulebaseReport.Id);

                    deviceAggregation.RuleCount++;
                }

                foreach ((int rulebaseId, List<Rule> ruleList) in rulebaseRules)
                {
                    RulebaseReport report = rulebaseReports[rulebaseId];
                    report.Rules = ruleList.ToArray();
                    report.RuleStatistics.ObjectAggregate.ObjectCount = report.Rules.Length;
                }

                managementReport.Rulebases = rulebaseReports.Values.OrderBy(rb => rb.Id).ToArray();

                managementReport.Devices = deviceAggregations
                                                .OrderBy(pair => pair.Key)
                                                .Select(pair => CreateDeviceReport(pair.Key, pair.Value))
                                                .ToArray();

                managementReports.Add(managementReport);
            }

            return managementReports;
        }

        private static int ResolveRulebaseId(Rule rule, Dictionary<long, int> pseudoRulebaseIds, ref int nextPseudoRulebaseId)
        {
            if (rule.RulebaseId != 0)
            {
                return rule.RulebaseId;
            }

            if (rule.Rulebase?.Id > 0)
            {
                return Convert.ToInt32(rule.Rulebase.Id);
            }

            long fallbackKey = rule.Id;
            if (!pseudoRulebaseIds.TryGetValue(fallbackKey, out int pseudoId))
            {
                pseudoId = nextPseudoRulebaseId--;
                pseudoRulebaseIds.Add(fallbackKey, pseudoId);
            }

            return pseudoId;
        }

        private static RulebaseReport GetOrCreateRulebaseReport(Dictionary<int, RulebaseReport> rulebaseReports, int rulebaseId, Rule rule)
        {
            if (rulebaseReports.TryGetValue(rulebaseId, out RulebaseReport? existing))
            {
                return existing;
            }

            string? rulebaseName = rule.Rulebase?.Name;
            if (string.IsNullOrEmpty(rulebaseName))
            {
                rulebaseName = rule.RulebaseName;
            }

            RulebaseReport newReport = new()
            {
                Id = rulebaseId,
                Name = rulebaseName
            };

            rulebaseReports.Add(rulebaseId, newReport);

            return newReport;
        }

        private static int ResolveDeviceId(Rule rule, Management? management, Dictionary<string, Device> deviceNameMap)
        {
            if (rule.Metadata?.DeviceId > 0)
            {
                return rule.Metadata.DeviceId;
            }

            if (rule.EnforcingGateways?.Length > 0)
            {
                Device? enforcingDevice = rule.EnforcingGateways
                                                .Select(wrapper => wrapper.Content)
                                                .FirstOrDefault(device => device?.Id > 0);
                if (enforcingDevice?.Id > 0)
                {
                    return enforcingDevice.Id;
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.DeviceName) && deviceNameMap.TryGetValue(rule.DeviceName, out Device? deviceByName) && deviceByName.Id > 0)
            {
                return deviceByName.Id;
            }

            if (management?.Devices?.Length == 1)
            {
                return management.Devices[0].Id;
            }

            return 0;
        }

        private static DeviceAggregation GetOrCreateDeviceAggregation(Dictionary<int, DeviceAggregation> deviceAggregations, int deviceId, Rule rule, Management? management, Dictionary<int, Device> deviceMap, Dictionary<string, Device> deviceNameMap)
        {
            if (!deviceAggregations.TryGetValue(deviceId, out DeviceAggregation? aggregation))
            {
                (string deviceName, string deviceUid) = ResolveDeviceMetadata(deviceId, rule, deviceMap, deviceNameMap);

                aggregation = new DeviceAggregation
                {
                    Name = deviceName,
                    Uid = deviceUid
                };

                deviceAggregations.Add(deviceId, aggregation);
            }
            else
            {
                RefreshDeviceAggregationMetadata(aggregation, deviceId, rule, deviceMap, deviceNameMap);
            }

            return aggregation;
        }

        private static (string deviceName, string deviceUid) ResolveDeviceMetadata(int deviceId, Rule rule, Dictionary<int, Device> deviceMap, Dictionary<string, Device> deviceNameMap)
        {
            if (deviceId > 0 && deviceMap.TryGetValue(deviceId, out Device? deviceFromManagement))
            {
                return (deviceFromManagement.Name ?? "", deviceFromManagement.Uid ?? "");
            }

            if (!string.IsNullOrWhiteSpace(rule.DeviceName) && deviceNameMap.TryGetValue(rule.DeviceName, out Device? deviceByName))
            {
                return (deviceByName.Name ?? "", deviceByName.Uid ?? "");
            }

            if (!string.IsNullOrWhiteSpace(rule.DeviceName))
            {
                return (rule.DeviceName, "");
            }

            return (deviceId > 0 ? $"Device {deviceId}" : "Unknown Device", "");
        }

        private static void RefreshDeviceAggregationMetadata(DeviceAggregation aggregation, int deviceId, Rule rule, Dictionary<int, Device> deviceMap, Dictionary<string, Device> deviceNameMap)
        {
            (string candidateName, string candidateUid) = ResolveDeviceMetadata(deviceId, rule, deviceMap, deviceNameMap);

            bool hasMeaningfulName = !string.IsNullOrWhiteSpace(candidateName);
            bool shouldReplaceName = string.IsNullOrWhiteSpace(aggregation.Name)
                                     || aggregation.Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase);

            if (hasMeaningfulName && shouldReplaceName)
            {
                aggregation.Name = candidateName;
            }

            if (!string.IsNullOrEmpty(candidateUid))
            {
                aggregation.Uid = candidateUid;
            }
        }

        private static void EnsureRulebaseLink(DeviceAggregation deviceAggregation, int deviceId, int rulebaseId)
        {
            if (deviceAggregation.RulebaseLinks.Any(link => link.NextRulebaseId == rulebaseId))
            {
                return;
            }

            int? initialRulebaseId = deviceAggregation.RulebaseLinks.FirstOrDefault()?.NextRulebaseId;

            RulebaseLink newLink = new()
            {
                GatewayId = deviceId,
                NextRulebaseId = rulebaseId,
                IsInitial = deviceAggregation.RulebaseLinks.Count == 0,
                FromRulebaseId = deviceAggregation.RulebaseLinks.Count == 0 ? null : initialRulebaseId,
                Removed = null
            };

            deviceAggregation.RulebaseLinks.Add(newLink);
        }

        private static DeviceReport CreateDeviceReport(int deviceId, DeviceAggregation aggregation)
        {
            DeviceReport deviceReport = new()
            {
                Id = deviceId,
                Name = aggregation.Name,
                Uid = aggregation.Uid,
                RulebaseLinks = aggregation.RulebaseLinks.ToArray()
            };

            deviceReport.RuleStatistics.ObjectAggregate.ObjectCount = aggregation.RuleCount;

            return deviceReport;
        }

        private sealed class DeviceAggregation
        {
            public string Name { get; set; } = "";
            public string Uid { get; set; } = "";
            public List<RulebaseLink> RulebaseLinks { get; } = [];
            public int RuleCount { get; set; }
        }
    }
}
