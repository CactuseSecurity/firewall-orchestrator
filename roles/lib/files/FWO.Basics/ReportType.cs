namespace FWO.Basics
{
    public enum ReportType
    {
        All = 0,
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
        VarianceAnalysis = 23,
        OwnerRecertification = 24
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

        public static bool IsConnectionRelatedReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.VarianceAnalysis => true,
                _ => false,
            };
        }

        public static bool IsModellingReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.AppRules or
                ReportType.VarianceAnalysis or
                ReportType.OwnerRecertification => true,
                _ => false,
            };
        }

        public static List<ReportType> AllReportTypes()
        {
            return [.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>().Where(r => r != ReportType.All)];
        }

        public static List<ReportType> ReportTypeSelection(bool ruleRelated = true, bool modellingRelated = true)
        {
            return CustomSortReportType([.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>()], ruleRelated, modellingRelated);
        }

        public static List<ReportType> CustomSortReportType(List<ReportType> ListIn, bool ruleRelated, bool modellingRelated)
        {
            List<ReportType> ListOut = [];
            List<ReportType> orderedReportTypeList =
            [
                ReportType.All,
                ReportType.Rules, ReportType.ResolvedRules, ReportType.ResolvedRulesTech, ReportType.UnusedRules, ReportType.NatRules,
                ReportType.Recertification,
                ReportType.Changes, ReportType.ResolvedChanges, ReportType.ResolvedChangesTech,
                ReportType.Statistics,
                ReportType.Connections,
                ReportType.AppRules,
                ReportType.VarianceAnalysis,
                ReportType.OwnerRecertification
            ];
            foreach (var reportType in orderedReportTypeList.Where(r => ListIn.Contains(r)))
            {
                if (reportType == ReportType.All || ruleRelated && reportType.IsDeviceRelatedReport() || modellingRelated && reportType.IsModellingReport())
                {
                    ListOut.Add(reportType);
                }
                ListIn.Remove(reportType);
            }
            // finally add remaining report types
            ListOut.AddRange(ListIn);
            return ListOut;
        }
    }
}
