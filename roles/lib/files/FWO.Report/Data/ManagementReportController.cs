using Newtonsoft.Json;
using System.Text.Json.Serialization;
using FWO.Data.Report;
using FWO.Data;

namespace FWO.Report
{
    public class ManagementReportController : ManagementReport
    {
        public ManagementReportController()
        { }

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
            foreach (var device in Devices.Select(dev => DeviceReportController.FromDeviceReport(dev)))
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
            foreach (var device in Devices.Select(dev => DeviceReportController.FromDeviceReport(dev)))
            {
                if (device.ContainsRules())
                {
                    return true;
                }
            }
            return false;
        }

    }
}
