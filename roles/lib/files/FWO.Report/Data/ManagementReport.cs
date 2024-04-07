using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.GlobalConstants;
using FWO.Api.Data;

namespace FWO.Report
{
    public class ManagementReport
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public DeviceReport[] Devices { get; set; } = Array.Empty<DeviceReport>();

        [JsonProperty("import"), JsonPropertyName("import")]
        public Import Import { get; set; } = new Import();

        public long? RelevantImportId { get; set; }

        [JsonProperty("networkObjects"), JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; } = Array.Empty<NetworkObject>();

        [JsonProperty("serviceObjects"), JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; } = Array.Empty<NetworkService>();

        [JsonProperty("userObjects"), JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; } = Array.Empty<NetworkUser>();

        [JsonProperty("reportNetworkObjects"), JsonPropertyName("reportNetworkObjects")]
        public NetworkObject[] ReportObjects { get; set; } = Array.Empty<NetworkObject>();

        [JsonProperty("reportServiceObjects"), JsonPropertyName("reportServiceObjects")]
        public NetworkService[] ReportServices { get; set; } = Array.Empty<NetworkService>();

        [JsonProperty("reportUserObjects"), JsonPropertyName("reportUserObjects")]
        public NetworkUser[] ReportUsers { get; set; } = Array.Empty<NetworkUser>();


        //[JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public List<long> ReportedRuleIds { get; set; } = new List<long>();
        public List<long> ReportedNetworkServiceIds { get; set; } = new List<long>();

        [JsonProperty("objects_aggregate"), JsonPropertyName("objects_aggregate")]
        public ObjectStatistics NetworkObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("services_aggregate"), JsonPropertyName("services_aggregate")]
        public ObjectStatistics ServiceObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("usrs_aggregate"), JsonPropertyName("usrs_aggregate")]
        public ObjectStatistics UserObjectStatistics { get; set; } = new ObjectStatistics();
        
        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

        public bool Ignore { get; set; }


        public ManagementReport()
        {}

        public ManagementReport(ManagementReport managementReport)
        {
            Id = managementReport.Id;
            Name = managementReport.Name;
            Devices = managementReport.Devices;
            Import = managementReport.Import;
            if (managementReport.Import != null && managementReport.Import.ImportAggregate != null &&
                managementReport.Import.ImportAggregate.ImportAggregateMax != null &&
                managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
            {
                RelevantImportId = managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
            }
            Objects = managementReport.Objects;
            Services = managementReport.Services;
            Users = managementReport.Users;
            ReportObjects = managementReport.ReportObjects;
            ReportServices = managementReport.ReportServices;
            ReportUsers = managementReport.ReportUsers;
            ReportedRuleIds = managementReport.ReportedRuleIds;
            ReportedNetworkServiceIds = managementReport.ReportedNetworkServiceIds;
            NetworkObjectStatistics = managementReport.NetworkObjectStatistics;
            ServiceObjectStatistics = managementReport.ServiceObjectStatistics;
            UserObjectStatistics = managementReport.UserObjectStatistics;
            RuleStatistics = managementReport.RuleStatistics;
            Ignore = managementReport.Ignore;
        }

        public void AssignRuleNumbers()
        {
            foreach (var device in Devices)
            {
                device.AssignRuleNumbers();
            }
        }

        public string NameAndDeviceNames(string separator = ", ")
        {
            return $"{Name} [{string.Join(separator, Array.ConvertAll(Devices, device => device.Name))}]";
        }
    }

    public static class ManagementUtility
    {
        public static bool Merge(this List<ManagementReport> managementReports, List<ManagementReport> managementReportsToMerge)
        {
            bool newObjects = false;

            foreach(var managementReportToMerge in managementReportsToMerge)
            {
                ManagementReport? mgmtToFill = managementReports.FirstOrDefault(m => m.Id == managementReportToMerge.Id);
                if(mgmtToFill!= null)
                {
                    newObjects |= mgmtToFill.Merge(managementReportToMerge);
                }
            }
            return newObjects;
        }

        public static bool Merge(this ManagementReport managementReport, ManagementReport managementToMerge)
        {
            bool newObjects = false;

            if (managementReport.Objects != null && managementToMerge.Objects != null && managementToMerge.Objects.Length > 0)
            {
                managementReport.Objects = managementReport.Objects.Concat(managementToMerge.Objects).ToArray();
                newObjects = true;
            }

            if (managementReport.Services != null && managementToMerge.Services != null && managementToMerge.Services.Length > 0)
            {
                managementReport.Services = managementReport.Services.Concat(managementToMerge.Services).ToArray();
                newObjects = true;
            }

            if (managementReport.Users != null && managementToMerge.Users != null && managementToMerge.Users.Length > 0)
            {
                managementReport.Users = managementReport.Users.Concat(managementToMerge.Users).ToArray();
                newObjects = true;
            }

            if (managementReport.Devices != null && managementToMerge.Devices != null && managementToMerge.Devices.Length > 0)
            {
                // important: if any management still returns rules, newObjects is set to true
                if (managementReport.Devices.Merge(managementToMerge.Devices) == true)
                    newObjects = true;
            }
            return newObjects;
        }

        public static bool MergeReportObjects(this ManagementReport managementReport, ManagementReport managementReportToMerge)
        {
            bool newObjects = false;

            if (managementReport.ReportObjects != null && managementReportToMerge.ReportObjects != null && managementReportToMerge.ReportObjects.Length > 0)
            {
                managementReport.ReportObjects = managementReport.ReportObjects.Concat(managementReportToMerge.ReportObjects).ToArray();
                newObjects = true;
            }

            if (managementReport.ReportServices != null && managementReportToMerge.ReportServices != null && managementReportToMerge.ReportServices.Length > 0)
            {
                managementReport.ReportServices = managementReport.ReportServices.Concat(managementReportToMerge.ReportServices).ToArray();
                newObjects = true;
            }

            if (managementReport.ReportUsers != null && managementReportToMerge.ReportUsers != null && managementReportToMerge.ReportUsers.Length > 0)
            {
                managementReport.ReportUsers = managementReport.ReportUsers.Concat(managementReportToMerge.ReportUsers).ToArray();
                newObjects = true;
            }

            if (managementReport.Devices != null && managementReportToMerge.Devices != null && managementReportToMerge.Devices.Length > 0)
            {
                // important: if any management still returns rules, newObjects is set to true
                if (managementReport.Devices.Merge(managementReportToMerge.Devices) == true)
                    newObjects = true;
            }
            return newObjects;
        }
    }
}
