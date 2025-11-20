using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ReportQueries : Queries
    {
        public static readonly string getReportTemplates;
        public static readonly string addReportTemplate;
        public static readonly string updateReportTemplate;
        public static readonly string deleteReportTemplate;

        public static readonly string subscribeReportScheduleChanges;
        public static readonly string addReportSchedule;
        public static readonly string addReportScheduleFileFormats;
        public static readonly string editReportSchedule;
        public static readonly string deleteReportSchedule;
        public static readonly string getReportSchedules;
        public static readonly string countReportSchedule;

        public static readonly string getReportsOverview;

        public static readonly string getReportsById;
        public static readonly string getRelevantImportIdsAtTime;
        public static readonly string getRelevantImportIdsInTimeRange;
        public static readonly string statisticsReportCurrent;

        public static readonly string subscribeGeneratedReportsChanges;
        public static readonly string getGeneratedReport;
        public static readonly string getGeneratedReports;
        public static readonly string deleteGeneratedReport;
        public static readonly string addGeneratedReport;

        public static readonly string getUsageDataCount;
        public static readonly string getImportsToNotify;
        public static readonly string setImportsNotified;

        public static readonly string getManagementForNormalizedConfig;
        public static readonly string getManagementForLatestNormalizedConfig;

        static ReportQueries()
        {
            try
            {
                addReportTemplate = GetQueryText("report/addReportTemplate.graphql");
                addReportSchedule = GetQueryText("report/addReportSchedule.graphql");
                addReportScheduleFileFormats = GetQueryText("report/addReportScheduleFileFormats.graphql");
                editReportSchedule = GetQueryText("report/editReportSchedule.graphql");
                deleteReportSchedule = GetQueryText("report/deleteReportSchedule.graphql");
                getReportSchedules = GetQueryText("report/getReportSchedules.graphql");
                countReportSchedule = GetQueryText("report/countReportSchedule.graphql");
                getReportsOverview = GetQueryText("report/getReportsOverview.graphql");
                getReportsById = GetQueryText("report/getReportById.graphql");
                getReportTemplates = GetQueryText("report/getReportTemplates.graphql");
                getRelevantImportIdsAtTime = GetQueryText("report/getRelevantImportIdsAtTime.graphql");
                getRelevantImportIdsInTimeRange = GetQueryText("report/getRelevantImportIdsInTimeRange.graphql");
                statisticsReportCurrent = GetQueryText("report/statisticsCurrent.graphql");
                statisticsReportCurrent = GetQueryText("report/statisticsCurrentOverall.graphql");
                updateReportTemplate = GetQueryText("report/updateReportTemplate.graphql");
                deleteReportTemplate = GetQueryText("report/deleteReportTemplate.graphql");
                subscribeReportScheduleChanges = GetQueryText("report/subscribeReportScheduleChanges.graphql");
                subscribeGeneratedReportsChanges = GetQueryText("report/subscribeGeneratedReportsChanges.graphql");
                getGeneratedReports = GetQueryText("report/getGeneratedReports.graphql");
                getGeneratedReport = GetQueryText("report/getGeneratedReport.graphql");
                deleteGeneratedReport = GetQueryText("report/deleteGeneratedReport.graphql");
                addGeneratedReport = GetQueryText("report/addGeneratedReport.graphql");
                getUsageDataCount = GetQueryText("report/getUsageDataCount.graphql");
                // note: currently we only check for rule changes, but this should be extended to other changes in the future
                // getImportsToNotify = GetQueryText("report/getImportsToNotifyForAnyChanges.phql");
                getImportsToNotify = GetQueryText("report/getImportsToNotifyForRuleChanges.graphql");
                setImportsNotified = GetQueryText("report/setImportsNotified.graphql");
                getManagementForNormalizedConfig = GetQueryText("report/getManagementForNormalizedConfig.graphql");
                getManagementForLatestNormalizedConfig = GetQueryText("report/getManagementForLatestNormalizedConfig.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api ReportQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
