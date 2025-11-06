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
                RuleDifferences.Add(new(){ModelledConnection = conn, ImplementedRules = [rule]});
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
                OkRules.Add(new(){ModelledConnection = conn, ImplementedRules = [rule]});
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
            foreach (var mgtId in rulesToReport.Keys.Where(m => rulesToReport[m].Count > 0))
            {
                Management? mgt = Managements.FirstOrDefault(m => m.Id == mgtId);
                ManagementReport managementReport = new() { Id = mgtId, Name = mgt?.Name ?? "" };
                List<DeviceReport> deviceReports = [];
                foreach (var rule in rulesToReport[mgtId])
                {
                    DeviceReport? existingDev = deviceReports.FirstOrDefault(d => d.Id == rule.DeviceId);
                    if (existingDev != null)
                    {
                        existingDev.Rules = existingDev.Rules?.Append(rule).ToArray();
                    }
                    else
                    {
                        string devName = mgt == null ? "" : mgt.Devices.FirstOrDefault(d => d.Id == rule.DeviceId)?.Name ?? "";
                        deviceReports.Add(new() { Id = rule.DeviceId, Name = devName, Rules = [rule] });
                    }
                }
                managementReport.Devices = [.. deviceReports];
                managementReports.Add(managementReport);
            }
            return managementReports;
        }
    }
}
