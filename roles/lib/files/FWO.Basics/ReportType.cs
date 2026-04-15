namespace FWO.Basics
{
    public enum ReportType
    {
        Undefined = 0,
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
        OwnerRecertification = 24,
        RecertificationEvent = 25,
        RecertEventReport = 26,

        ComplianceReport = 31,
        ComplianceDiffReport = 32,

        TicketReport = 41,
        TicketChangeReport = 42
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
                ReportType.AppRules or
                ReportType.RecertEventReport => true,
                _ => false
            };
        }

        public static bool IsChangeReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Changes or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech => true,
                _ => false
            };
        }

        public static bool IsResolvedReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech or
                ReportType.ComplianceReport or
                ReportType.ComplianceDiffReport => true,
                _ => false,
            };
        }

        public static bool IsTechReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.ResolvedRulesTech or
                ReportType.ResolvedChangesTech => true,
                _ => false
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
                ReportType.VarianceAnalysis or
                ReportType.RecertificationEvent => true,
                _ => false
            };
        }

        public static bool IsModellingReport(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Connections or
                ReportType.AppRules or
                ReportType.VarianceAnalysis or
                ReportType.OwnerRecertification or
                ReportType.RecertificationEvent or
                ReportType.RecertEventReport => true,
                _ => false
            };
        }

        public static bool IsComplianceReport(this ReportType reportType)
        {
            return reportType == ReportType.ComplianceReport || reportType == ReportType.ComplianceDiffReport;
        }

        public static bool IsRulebaseReport(this ReportType reportType)
        {
            return reportType == ReportType.Recertification || reportType == ReportType.AppRules;
        }

        public static bool IsWorkflowReport(this ReportType reportType)
        {
            return reportType == ReportType.TicketReport || reportType == ReportType.TicketChangeReport;
        }

        public static bool HasTimeFilter(this ReportType reportType)
        {
            return reportType switch
            {
                ReportType.Rules or
                ReportType.ResolvedRules or
                ReportType.ResolvedRulesTech or
                ReportType.NatRules or
                ReportType.Statistics or
                ReportType.Changes or
                ReportType.ResolvedChanges or
                ReportType.ResolvedChangesTech or
                ReportType.TicketChangeReport => true,
                _ => false
            };
        }

        public static bool SupportsCsvExport(this ReportType reportType, bool detailedView = false)
        {
            return reportType.IsResolvedReport()
                || reportType.IsComplianceReport()
                || reportType == ReportType.OwnerRecertification
                || reportType.IsWorkflowReport() && !detailedView;
        }

        /// <summary>
        /// Determines whether a report type supports HTML export.
        /// </summary>
        public static bool SupportsHtmlExport(this ReportType reportType)
        {
            return !reportType.IsComplianceReport();
        }

        /// <summary>
        /// Determines whether a report type supports PDF export.
        /// </summary>
        public static bool SupportsPdfExport(this ReportType reportType)
        {
            return reportType.SupportsHtmlExport();
        }

        public static List<ReportType> AllReportTypes()
        {
            return [.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>().Where(r => r != ReportType.Undefined)];
        }

        public static List<ReportType> ReportTypeSelection(bool ruleRelated = true, bool modellingRelated = true)
        {
            return CustomSortReportType([.. Enum.GetValues(typeof(ReportType)).Cast<ReportType>()], ruleRelated, modellingRelated);
        }

        public static bool IsVisibleTemplateType(this ReportType reportType, bool ruleRelated, bool modellingRelated, bool complianceRelated, bool modellingOwnerAllowed = true)
        {
            return ruleRelated && reportType.IsDeviceRelatedReport()
                || modellingRelated && reportType.IsModellingReport() && modellingOwnerAllowed
                || complianceRelated && reportType.IsComplianceReport()
                || reportType.IsWorkflowReport();
        }

        public static List<ReportType> CustomSortReportType(List<ReportType> ListIn, bool ruleRelated, bool modellingRelated)
        {
            List<ReportType> ListOut = [];
            List<ReportType> orderedReportTypeList =
            [
                ReportType.Undefined,
                ReportType.RecertificationEvent,
                ReportType.Rules, ReportType.ResolvedRules, ReportType.ResolvedRulesTech, ReportType.UnusedRules, ReportType.NatRules,
                ReportType.Changes, ReportType.ResolvedChanges, ReportType.ResolvedChangesTech,
                ReportType.Statistics,
                ReportType.Connections,
                ReportType.AppRules,
                ReportType.VarianceAnalysis,
                ReportType.Recertification,
                ReportType.OwnerRecertification,
                ReportType.RecertEventReport,
                ReportType.TicketReport,
                ReportType.TicketChangeReport
            ];
            foreach (var reportType in orderedReportTypeList.Where(r => ListIn.Contains(r)))
            {
                if (reportType == ReportType.Undefined || reportType.IsVisibleTemplateType(ruleRelated, modellingRelated, false))
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
