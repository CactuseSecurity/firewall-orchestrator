using FWO.Basics.Interfaces;
using FWO.Data.Workflow;

namespace FWO.Data.Report
{
    public class ReportData
    {
        public List<ManagementReport> ManagementData { get; set; } = [];
        public List<OwnerConnectionReport> OwnerData { get; set; } = [];
        public List<GlobalCommonSvcReport> GlobalComSvc { get; set; } = [];
        public ManagementReport GlobalStats { get; set; } = new();
        /// <summary>
        /// Gets or sets the workflow tickets contained in a workflow report.
        /// </summary>
        public List<WfTicket> Tickets { get; set; } = [];
        public Dictionary<int, string> WorkflowStateNames { get; set; } = [];
        public WorkflowFilter WorkflowFilter { get; set; } = new();
        public List<Rule> RulesFlat = [];
        public IEnumerable<IRuleViewData> RuleViewData = [];
        public int ElementsCount { get; set; }
        public int RecertificationDisplayPeriod { get; set; } = 0;

        public ReportData()
        { }

        public ReportData(ReportData reportData)
        {
            ManagementData = reportData.ManagementData;
            OwnerData = reportData.OwnerData;
            GlobalComSvc = reportData.GlobalComSvc;
            GlobalStats = reportData.GlobalStats;
            Tickets = reportData.Tickets;
            WorkflowStateNames = reportData.WorkflowStateNames;
            WorkflowFilter = reportData.WorkflowFilter;
            RecertificationDisplayPeriod = reportData.RecertificationDisplayPeriod;
        }
    }
}
