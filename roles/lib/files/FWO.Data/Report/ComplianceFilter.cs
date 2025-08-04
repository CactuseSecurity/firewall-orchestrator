namespace FWO.Data.Report
{
    public class ComplianceFilter
    {
        public bool IsDiffReport { get; set; } = false;
        public int DiffReferenceInDays { get; set; } = 0;
        public bool ShowCompliantRules { get; set; } = false;
        List<string> ExcludedRuleActions { get; set; } = [];

        public ComplianceFilter()
        {

        }

        public ComplianceFilter(ComplianceFilter complianceFilter)
        {
            IsDiffReport = complianceFilter.IsDiffReport;
            DiffReferenceInDays = complianceFilter.DiffReferenceInDays;
            ShowCompliantRules = complianceFilter.ShowCompliantRules;
            ExcludedRuleActions = complianceFilter.ExcludedRuleActions;
        }
    }
    
}