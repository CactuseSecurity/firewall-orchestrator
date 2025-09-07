namespace FWO.Data.Report
{
    public class ReportData
    {
        public List<ManagementReport> ManagementData { get; set; } = [];
        public List<OwnerConnectionReport> OwnerData { get; set; } = [];
        public List<GlobalCommonSvcReport> GlobalComSvc { get; set; } = [];
        public ManagementReport GlobalStats { get; set; } = new();


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
