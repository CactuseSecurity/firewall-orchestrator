﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class ReportQueries : Queries
    {
        public static readonly string getReportTemplates;
        public static readonly string addReportTemplate;
        public static readonly string editReportTemplate;
        public static readonly string deleteReportTemplate;

        public static readonly string subscribeReportScheduleChanges;
        public static readonly string addReportSchedule;
        public static readonly string addReportScheduleFileFormats;
        public static readonly string editReportSchedule;
        public static readonly string deleteReportSchedule;
        public static readonly string getReportSchedules;

        public static readonly string getReportsOverview;

        public static readonly string getReportsById;
        public static readonly string getRelevantImportIdsAtTime;
        public static readonly string statisticsReportCurrent;

        public static readonly string getGeneratedReport;
        public static readonly string getGeneratedReports;
        public static readonly string deleteGeneratedReport;
        public static readonly string addGeneratedReport;

        static ReportQueries() 
        {
            try
            {
                addReportTemplate = File.ReadAllText(QueryPath + "report/addReportTemplate.graphql");
                addReportSchedule = File.ReadAllText(QueryPath + "report/addReportSchedule.graphql");
                addReportScheduleFileFormats = File.ReadAllText(QueryPath + "report/addReportScheduleFileFormats.graphql");
                editReportSchedule = File.ReadAllText(QueryPath + "report/editReportSchedule.graphql");
                deleteReportSchedule = File.ReadAllText(QueryPath + "report/deleteReportSchedule.graphql");
                getReportSchedules = File.ReadAllText(QueryPath + "report/getReportSchedules.graphql");
                getReportsOverview = File.ReadAllText(QueryPath + "report/getReportsOverview.graphql");
                getReportsById = File.ReadAllText(QueryPath + "report/getReportById.graphql");
                getReportTemplates = File.ReadAllText(QueryPath + "report/getReportTemplates.graphql");
                getRelevantImportIdsAtTime = File.ReadAllText(QueryPath + "report/getRelevantImportIdsAtTime.graphql");
                statisticsReportCurrent = File.ReadAllText(QueryPath + "report/statisticsCurrent.graphql");
                statisticsReportCurrent = File.ReadAllText(QueryPath + "report/statisticsCurrentOverall.graphql");
                editReportTemplate = File.ReadAllText(QueryPath + "report/editReportTemplate.graphql");
                deleteReportTemplate = File.ReadAllText(QueryPath + "report/deleteReportTemplate.graphql");
                subscribeReportScheduleChanges = File.ReadAllText(QueryPath + "report/subscribeReportScheduleChanges.graphql");
                getGeneratedReports = File.ReadAllText(QueryPath + "report/getGeneratedReports.graphql");
                getGeneratedReport = File.ReadAllText(QueryPath + "report/getGeneratedReport.graphql");
                deleteGeneratedReport = File.ReadAllText(QueryPath + "report/deleteGeneratedReport.graphql");
                addGeneratedReport = File.ReadAllText(QueryPath + "report/addGeneratedReport.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api ReportQueries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
