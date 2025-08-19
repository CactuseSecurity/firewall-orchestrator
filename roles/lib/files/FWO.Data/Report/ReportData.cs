namespace FWO.Data.Report
{
    public class ReportData
    {
        public List<ManagementReport> ManagementData = [];
        public List<OwnerReport> OwnerData = [];
        public List<GlobalCommonSvcReport> GlobalComSvc = [];
        public ManagementReport GlobalStats = new();
        public List<Rule> RulesFlat = [];
        public int ElementsCount { get; set; }


        public ReportData()
        { }

        public ReportData(ReportData reportData)
        {
            ManagementData = reportData.ManagementData;
            OwnerData = reportData.OwnerData;
            GlobalComSvc = reportData.GlobalComSvc;
            GlobalStats = reportData.GlobalStats;
        }
    }
}
