using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.Data.Report;
using FWO.Data;

namespace FWO.Report
{
    public class ManagementReportController : ManagementReport
    {
        public ManagementReportController()
        {}

        public ManagementReportController(ManagementReport managementReport)
        {
            Id = managementReport.Id;
            Name = managementReport.Name;
            Devices = managementReport.Devices;
            Rulebases = managementReport.Rulebases;
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
            RelevantObjectIds = managementReport.RelevantObjectIds;
            HighlightedObjectIds = managementReport.HighlightedObjectIds;
        }

        public void AssignRuleNumbers()
        {
            foreach (DeviceReportController device in Devices.Cast<DeviceReportController>())
            {
                device.AssignRuleNumbers();
            }
        }

        public string NameAndRulebaseNames(string separator = ", ")
        {
            return $"{Name} [{string.Join(separator, Array.ConvertAll(Devices, device => device.Name))}]";
        }
        public bool ContainsRules()
        {
            foreach (DeviceReportController device in Devices.Cast<DeviceReportController>())
            {
                if (device.ContainsRules())
                {
                    return true;
                }
            }
            return false;
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

            if (managementReport.Rulebases != null && managementToMerge.Rulebases != null && managementToMerge.Rulebases.Length > 0)
            {
                // important: if any management still returns rules, newObjects is set to true
                if (managementReport.Rulebases.Merge(managementToMerge.Rulebases) == true)
                    newObjects = true;
            }

            // if (managementReport.Devices != null && managementToMerge.Devices!= null && managementToMerge.Devices.Length > 0)
            // {
            //     managementReport.Devices = managementReport.Devices.Concat(managementToMerge.Devices).ToArray();
            //     newObjects = true;
            // }

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
                if (managementReport.Rulebases.Merge(managementReportToMerge.Rulebases) == true)
                    newObjects = true;
            }
            return newObjects;
        }

    }
}
