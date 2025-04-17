namespace FWO.Basics
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
        ResolvedChangesTech = 9,
        UnusedRules = 10,

        Connections = 21,
        AppRules = 22,
        VarianceAnalysis = 23
    }

    public static class ReportTypeGroups
    {
        public static bool IsRuleReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Rules or
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.NatRules or
                ReportType.Recertification or
                ReportType.UnusedRules or
                ReportType.AppRules => true,
                _ => false,
            };
        }

        public static bool IsChangeReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Changes or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech => true,
                _ => false,
            };
        }

        public static bool IsResolvedReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech => true,
                _ => false,
            };
        }

        public static bool IsTechReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChangesTech => true,
                _ => false,
            };
        }

        public static bool IsDeviceRelatedReport(this ReportType reportType)
        {
            return reportType.IsRuleReport() || reportType.IsChangeReport() || reportType == ReportType.Statistics;
        }

        public static bool IsOwnerRelatedReport(this ReportType reportType)
        {
            return reportType == ReportType.Connections || reportType == ReportType.VarianceAnalysis;
        }

        public static bool IsModellingReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.AppRules or
                ReportType.VarianceAnalysis => true,
                _ => false,
            };
        }
    }
}
