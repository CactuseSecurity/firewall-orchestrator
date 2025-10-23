using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Report
{
    public class ManagementReport
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public DeviceReport[] Devices { get; set; } = [];

        [JsonProperty("rulebases"), JsonPropertyName("rulebases")]
        public RulebaseReport[] Rulebases { get; set; } = [];

        [JsonProperty("import"), JsonPropertyName("import")]
        public Import Import { get; set; } = new();

        [JsonProperty("import_controls"), JsonPropertyName("import_controls")]
        public List<ImportControl> ImportControls { get; set; } = [];

        public long? RelevantImportId { get; set; }

        [JsonProperty("managementByMultiDeviceManagerId"), JsonPropertyName("managementByMultiDeviceManagerId")]
        public List<Management> SubManagements { get; set; } = [];

        [JsonProperty("networkObjects"), JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; } = [];

        [JsonProperty("serviceObjects"), JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; } = [];

        [JsonProperty("userObjects"), JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; } = [];

        [JsonProperty("zoneObjects"), JsonPropertyName("zoneObjects")]
        public NetworkZone[] Zones { get; set; } = [];

        [JsonProperty("reportNetworkObjects"), JsonPropertyName("reportNetworkObjects")]
        public NetworkObject[] ReportObjects { get; set; } = [];

        [JsonProperty("reportServiceObjects"), JsonPropertyName("reportServiceObjects")]
        public NetworkService[] ReportServices { get; set; } = [];

        [JsonProperty("reportUserObjects"), JsonPropertyName("reportUserObjects")]
        public NetworkUser[] ReportUsers { get; set; } = [];


        //[JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public List<long> ReportedRuleIds { get; set; } = [];
        public List<long> ReportedNetworkServiceIds { get; set; } = [];

        [JsonProperty("objects_aggregate"), JsonPropertyName("objects_aggregate")]
        public ObjectStatistics NetworkObjectStatistics { get; set; } = new();

        [JsonProperty("services_aggregate"), JsonPropertyName("services_aggregate")]
        public ObjectStatistics ServiceObjectStatistics { get; set; } = new();

        [JsonProperty("usrs_aggregate"), JsonPropertyName("usrs_aggregate")]
        public ObjectStatistics UserObjectStatistics { get; set; } = new();

        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new();

        public bool Ignore { get; set; }
        public List<long> RelevantObjectIds = [];
        public List<long> HighlightedObjectIds = [];

        public bool[] Detailed = [false, false, false]; // nobj, nsrv, user

        public ManagementReport()
        { }


        public string NameAndDeviceNames(string separator = ", ")
        {
            return $"{Name} [{string.Join(separator, Array.ConvertAll(Devices, device => device.Name))}]";
        }

    }
    public static class ManagementUtility
    {
        public static (bool, Dictionary<string, int>) Merge(this List<ManagementReport> managementReports, List<ManagementReport> managementReportsToMerge)
        {
            bool newObjects = false;
            Dictionary<string, int> maxAddedCounts = new()
            {
                { "NetworkObjects", 0 },
                { "NetworkServices", 0 },
                { "NetworkUsers", 0 },
                { "Rules", 0 },
                { "RuleChanges", 0 },
            };

            foreach(var managementReportToMerge in managementReportsToMerge)
            {
                ManagementReport? mgmtToFill = managementReports.FirstOrDefault(m => m.Id == managementReportToMerge.Id);
                if(mgmtToFill!= null)
                {
                    (bool newObjs, Dictionary<string, int> addedCounts) = mgmtToFill.Merge(managementReportToMerge);
                    if (newObjs)
                    {
                        newObjects = true;
                        maxAddedCounts["NetworkObjects"] = Math.Max(maxAddedCounts["NetworkObjects"], addedCounts["NetworkObjects"]);
                        maxAddedCounts["NetworkServices"] = Math.Max(maxAddedCounts["NetworkServices"], addedCounts["NetworkServices"]);
                        maxAddedCounts["NetworkUsers"] = Math.Max(maxAddedCounts["NetworkUsers"], addedCounts["NetworkUsers"]);
                        maxAddedCounts["Rules"] = Math.Max(maxAddedCounts["Rules"], addedCounts["Rules"]);
                        maxAddedCounts["RuleChanges"] = Math.Max(maxAddedCounts["RuleChanges"], addedCounts["RuleChanges"]);
                    }
                }
            }
            return (newObjects, maxAddedCounts);
        }

        public static (bool, Dictionary<string, int>) Merge(this ManagementReport managementReport, ManagementReport managementReportToMerge)
        {
            bool newObjects = false;
            Dictionary<string, int> maxAddedCounts = new()
            {
                { "NetworkObjects", 0 },
                { "NetworkServices", 0 },
                { "NetworkUsers", 0 },
                { "Rules", 0 },
                { "RuleChanges", 0 },
            };

            if (managementReport.Objects != null && managementReportToMerge.Objects != null && managementReportToMerge.Objects.Length > 0)
            {
                managementReport.Objects = [.. managementReport.Objects, .. managementReportToMerge.Objects];
                newObjects = true;
                maxAddedCounts["NetworkObjects"] = managementReportToMerge.Objects.Length;
            }

            if (managementReport.Services != null && managementReportToMerge.Services != null && managementReportToMerge.Services.Length > 0)
            {
                managementReport.Services = [.. managementReport.Services, .. managementReportToMerge.Services];
                newObjects = true;
                maxAddedCounts["NetworkServices"] = managementReportToMerge.Services.Length;
            }

            if (managementReport.Users != null && managementReportToMerge.Users != null && managementReportToMerge.Users.Length > 0)
            {
                managementReport.Users = [.. managementReport.Users, .. managementReportToMerge.Users];
                newObjects = true;
                maxAddedCounts["NetworkUsers"] = managementReportToMerge.Users.Length;
            }

            if (managementReport.ReportObjects != null && managementReportToMerge.ReportObjects != null && managementReportToMerge.ReportObjects.Length > 0)
            {
                managementReport.ReportObjects = managementReport.ReportObjects.Concat(managementReportToMerge.ReportObjects).ToArray();
                newObjects = true;
                maxAddedCounts["ReportObjects"] = managementReportToMerge.ReportObjects.Length;
            }

            if (managementReport.ReportServices != null && managementReportToMerge.ReportServices != null && managementReportToMerge.ReportServices.Length > 0)
            {
                managementReport.ReportServices = managementReport.ReportServices.Concat(managementReportToMerge.ReportServices).ToArray();
                newObjects = true;
                maxAddedCounts["ReportServices"] = managementReportToMerge.ReportServices.Length;
            }

            if (managementReport.ReportUsers != null && managementReportToMerge.ReportUsers != null && managementReportToMerge.ReportUsers.Length > 0)
            {
                managementReport.ReportUsers = managementReport.ReportUsers.Concat(managementReportToMerge.ReportUsers).ToArray();
                newObjects = true;
                maxAddedCounts["ReportUsers"] = managementReportToMerge.ReportUsers.Length;
            }

            for (int i = 0; i < managementReport.Rulebases.Length; i++)
            {
                RulebaseReport rulebaseReport = managementReport.Rulebases[i];
                RulebaseReport rulebaseReportToMerge = managementReportToMerge.Rulebases.First(rbr => rbr.Id == rulebaseReport.Id);
                if (rulebaseReportToMerge.Rules.Length > 0)
                {
                    rulebaseReport.Rules = [.. rulebaseReport.Rules, .. rulebaseReportToMerge.Rules];
                    newObjects = true;
                    maxAddedCounts["Rules"] = Math.Max(maxAddedCounts["Rules"], rulebaseReportToMerge.Rules.Length);
                }
            }

            return (newObjects, maxAddedCounts);
        }
    }
}
