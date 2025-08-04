namespace FWO.Data.Report
{
    public class ComplianceFilter
    {
        public bool IsDiffReport { get; set; } = false;
        public int DiffReferenceInDays { get; set; } = 0;
        public bool ShowCompliantRules { get; set; } = false;
        List<RuleAction> ExcludedRuleActions { get; set; } = [];

        public ComplianceFilter()
        {

        }

        public ComplianceFilter(ComplianceFilter complianceFilter)
        {
            IsDiffReport = complianceFilter.IsDiffReport;
            ShowCompliantRules = complianceFilter.ShowCompliantRules;
            ExcludedRuleActions = complianceFilter.ExcludedRuleActions;
        }
    }
    
}