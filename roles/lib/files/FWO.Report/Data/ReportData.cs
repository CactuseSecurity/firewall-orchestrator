using FWO.Api.Data;

namespace FWO.Report
{
    public class ReportData
    {
        public List<ManagementReport> ManagementData = new();
        public List<OwnerReport> OwnerData = new();
        public List<ModellingConnection> GlobalComSvc = new();
        public ManagementReport GlobalStats = new();


        public ReportData()
        {}

        public ReportData(ReportData reportData)
        {
            ManagementData = reportData.ManagementData;
            OwnerData = reportData.OwnerData;
            GlobalComSvc = reportData.GlobalComSvc;
            GlobalStats = reportData.GlobalStats;
        }
    }
}
