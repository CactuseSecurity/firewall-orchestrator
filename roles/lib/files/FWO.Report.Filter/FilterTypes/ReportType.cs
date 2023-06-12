namespace FWO.Report.Filter
{
    public enum ReportType
    {
        Rules = 1,
        Changes = 2,
        Statistics = 3,
        NatRules = 4,
        ResolvedRules = 5,
        ResolvedRulesTech = 6,
        Recertification = 7,
        ResolvedChanges = 8,
        ResolvedChangesTech = 9
    }

    public static class ReportTypeGroups
    {
        public static bool IsRuleReport(this ReportType reportType)
        {
            switch(reportType)
            {
                case ReportType.Rules:
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                case ReportType.NatRules:
                case ReportType.Recertification:
                    return true;
                default: 
                    return false;
            }
        }

        public static bool IsChangeReport(this ReportType reportType)
        {
            switch(reportType)
            {
                case ReportType.Changes:
                case ReportType.ResolvedChanges:
                case ReportType.ResolvedChangesTech:
                    return true;
                default: 
                    return false;
            }
        }

        public static bool IsResolvedReport(this ReportType reportType)
        {
            switch(reportType)
            {
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                case ReportType.ResolvedChanges:
                case ReportType.ResolvedChangesTech:
                    return true;
                default: 
                    return false;
            }
        }

        public static bool IsTechReport(this ReportType reportType)
        {
            switch(reportType)
            {
                case ReportType.ResolvedRulesTech:
                case ReportType.ResolvedChangesTech:
                    return true;
                default: 
                    return false;
            }
        }
    }
}
