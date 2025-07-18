﻿using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class MonitorQueries : Queries
    {
        public static readonly string addLogEntry;
        public static readonly string getLogEntrys;
        public static readonly string addUiLogEntry;
        public static readonly string getUiLogEntrys;
        public static readonly string getAllUiLogEntrys;
        public static readonly string getImportLogEntrys;
        public static readonly string addAlert;
        public static readonly string getOpenAlerts;
        public static readonly string getAlerts;
        public static readonly string getAlertById;
        public static readonly string acknowledgeAlert;
        public static readonly string subscribeAlertChanges;
        public static readonly string addAutodiscoveryLogEntry;
        public static readonly string getAutodiscoveryLogEntrys;
        public static readonly string getDailyCheckLogEntrys;
        public static readonly string addDataImportLogEntry;
        public static readonly string getDataImportLogEntrys;
        public static readonly string getImportStatus;
        public static readonly string getOwnerTickets;


        static MonitorQueries()
        {
            try
            {
                addLogEntry = File.ReadAllText(QueryPath + "monitor/addLogEntry.graphql");
                getLogEntrys = File.ReadAllText(QueryPath + "monitor/getLogEntrys.graphql");

                addUiLogEntry = File.ReadAllText(QueryPath + "monitor/addUiLogEntry.graphql");
                getUiLogEntrys = File.ReadAllText(QueryPath + "monitor/getUiLogEntrys.graphql");
                getAllUiLogEntrys = File.ReadAllText(QueryPath + "monitor/getAllUiLogEntrys.graphql");

                getImportLogEntrys = File.ReadAllText(QueryPath + "monitor/getImportLogEntrys.graphql");

                addAlert = File.ReadAllText(QueryPath + "monitor/addAlert.graphql");
                getOpenAlerts = File.ReadAllText(QueryPath + "monitor/getOpenAlerts.graphql");
                getAlerts = File.ReadAllText(QueryPath + "monitor/getAlerts.graphql");
                getAlertById = File.ReadAllText(QueryPath + "monitor/getAlertById.graphql");
                acknowledgeAlert = File.ReadAllText(QueryPath + "monitor/acknowledgeAlert.graphql");
                subscribeAlertChanges = File.ReadAllText(QueryPath + "monitor/subscribeAlertChanges.graphql");

                getImportStatus = File.ReadAllText(QueryPath + "monitor/getImportStatus.graphql");

                addAutodiscoveryLogEntry = File.ReadAllText(QueryPath + "monitor/addAutodiscoveryLogEntry.graphql");
                getAutodiscoveryLogEntrys = File.ReadAllText(QueryPath + "monitor/getAutodiscoveryLogEntrys.graphql");
                getDailyCheckLogEntrys = File.ReadAllText(QueryPath + "monitor/getDailyCheckLogEntrys.graphql");
                addDataImportLogEntry = File.ReadAllText(QueryPath + "monitor/addDataImportLogEntry.graphql");
                getDataImportLogEntrys = File.ReadAllText(QueryPath + "monitor/getDataImportLogEntrys.graphql");

                getOwnerTickets = RequestQueries.ticketOverviewFragment + File.ReadAllText(QueryPath + "monitor/getOwnerTickets.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize MonitorQueries", "Api MonitorQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
