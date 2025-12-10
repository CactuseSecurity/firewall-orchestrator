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
        public Dictionary<int, List<DeviceReport>> DeviceRules { get; set; } = [];


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
                if(mgt != null)
                {
                    ManagementReport managementReport = new() { Id = mgtId, Name = mgt.Name ?? "" };
                    List<DeviceReport> deviceReports = [];
                    foreach(var dev in mgt.Devices)
                    {
                        DeviceReport devReport = new() { Id = dev.Id, Name = dev.Name };
                        devReport.SetRulesForDev(FilterRulesForDev(rulesToReport[mgtId], mgtId, dev));
                        deviceReports.Add(devReport);
                    }
                    managementReport.Devices = [.. deviceReports];
                    managementReports.Add(managementReport);
                }
            }
            return managementReports;
        }

        public List<Rule> FilterRulesForDev(List<Rule> rulesToReport, int mgtId, Device device)
        {
            DeviceReport? devReport = DeviceRules[mgtId]?.FirstOrDefault(d => d.Id == device.Id);
            if(devReport != null)
            {
                return [.. rulesToReport.Where(devReport.IsLinked)];
            }
            return [];
        }
    }
}
