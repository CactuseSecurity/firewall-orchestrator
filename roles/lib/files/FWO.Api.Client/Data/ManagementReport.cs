using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ManagementReport : Management
    {
        [JsonProperty("networkObjects"), JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; } = new NetworkObject[]{};

        [JsonProperty("serviceObjects"), JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; } = new NetworkService[]{};

        [JsonProperty("userObjects"), JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; } = new NetworkUser[]{};

        [JsonProperty("reportNetworkObjects"), JsonPropertyName("reportNetworkObjects")]
        public NetworkObject[] ReportObjects { get; set; } = new NetworkObject[]{};

        [JsonProperty("reportServiceObjects"), JsonPropertyName("reportServiceObjects")]
        public NetworkService[] ReportServices { get; set; } = new NetworkService[]{};

        [JsonProperty("reportUserObjects"), JsonPropertyName("reportUserObjects")]
        public NetworkUser[] ReportUsers { get; set; } = new NetworkUser[]{};


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

        public ManagementReport()
        {}

        public ManagementReport(ManagementReport managementReport) : base(managementReport)
        {
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
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            return shortened;
        }
    }

    public static class ManagementUtility
    {
        public static bool Merge(this ManagementReport[] managements, ManagementReport[] managementsToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < managementsToMerge.Length; i++)
                newObjects |= managements[i].Merge(managementsToMerge[i]);

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

        public static bool MergeReportObjects(this ManagementReport managementReport, ManagementReport managementToMerge)
        {
            bool newObjects = false;

            if (managementReport.ReportObjects != null && managementToMerge.ReportObjects != null && managementToMerge.ReportObjects.Length > 0)
            {
                managementReport.ReportObjects = managementReport.ReportObjects.Concat(managementToMerge.ReportObjects).ToArray();
                newObjects = true;
            }

            if (managementReport.ReportServices != null && managementToMerge.ReportServices != null && managementToMerge.ReportServices.Length > 0)
            {
                managementReport.ReportServices = managementReport.ReportServices.Concat(managementToMerge.ReportServices).ToArray();
                newObjects = true;
            }

            if (managementReport.ReportUsers != null && managementToMerge.ReportUsers != null && managementToMerge.ReportUsers.Length > 0)
            {
                managementReport.ReportUsers = managementReport.ReportUsers.Concat(managementToMerge.ReportUsers).ToArray();
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
    }
}
