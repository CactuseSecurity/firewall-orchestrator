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
                getReportSchedules = File.ReadAllText(QueryPath + "report/getReportSchedules.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api ReportQueries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
