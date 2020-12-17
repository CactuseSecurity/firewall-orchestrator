using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class ReportQueries : Queries
    {
        public static readonly string addReportTemplate;
        public static readonly string addReportSchedule;
        public static readonly string getReportSchedules;
        public static readonly string getReportTemplates;
        public static readonly string getReportsOverview;
        public static readonly string getReportsById;
        public static readonly string getRelevantImportIdsAtTime;
        // public static readonly string getRuleChangeDetails;
        public static readonly string statisticsReportCurrent;

        static ReportQueries() 
        {
            try
            {
                addReportTemplate = File.ReadAllText(QueryPath + "report/addReportTemplate.graphql");
                addReportSchedule = File.ReadAllText(QueryPath + "report/addReportSchedule.graphql");
                getReportsOverview = File.ReadAllText(QueryPath + "report/getReportSchedules.graphql");
                getReportsById = File.ReadAllText(QueryPath + "report/getReportById.graphql");
                getReportTemplates = File.ReadAllText(QueryPath + "report/getReportTemplates.graphql");
                getRelevantImportIdsAtTime = File.ReadAllText(QueryPath + "report/getRelevantImportIdsAtTime.graphql");
                statisticsReportCurrent = File.ReadAllText(QueryPath + "report/statisticsCurrent.graphql");
                statisticsReportCurrent = File.ReadAllText(QueryPath + "report/statisticsCurrentOverall.graphql");
                getReportSchedules = File.ReadAllText(QueryPath + "report/getReportSchedules.graphql");

                // getRuleChangeDetails =
                //     File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") +
                //     File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql") +
                //     File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") +
                //     // File.ReadAllText(QueryPath + "rule/fragments/ruleDetails.graphql") +
                //     File.ReadAllText(QueryPath + "rule/fragments/ruleOverview.graphql") +
                //     File.ReadAllText(QueryPath + "report/getRuleChangeDetails.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api ReportQueries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
