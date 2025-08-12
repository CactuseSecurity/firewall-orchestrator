using FWO.Logging;

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
                addLogEntry = GetQueryText("monitor/addLogEntry.graphql");
                getLogEntrys = GetQueryText("monitor/getLogEntrys.graphql");

                addUiLogEntry = GetQueryText("monitor/addUiLogEntry.graphql");
                getUiLogEntrys = GetQueryText("monitor/getUiLogEntrys.graphql");
                getAllUiLogEntrys = GetQueryText("monitor/getAllUiLogEntrys.graphql");

                getImportLogEntrys = GetQueryText("monitor/getImportLogEntrys.graphql");

                addAlert = GetQueryText("monitor/addAlert.graphql");
                getOpenAlerts = GetQueryText("monitor/getOpenAlerts.graphql");
                getAlerts = GetQueryText("monitor/getAlerts.graphql");
                getAlertById = GetQueryText("monitor/getAlertById.graphql");
                acknowledgeAlert = GetQueryText("monitor/acknowledgeAlert.graphql");
                subscribeAlertChanges = GetQueryText("monitor/subscribeAlertChanges.graphql");

                getImportStatus = GetQueryText("monitor/getImportStatus.graphql");

                addAutodiscoveryLogEntry = GetQueryText("monitor/addAutodiscoveryLogEntry.graphql");
                getAutodiscoveryLogEntrys = GetQueryText("monitor/getAutodiscoveryLogEntrys.graphql");
                getDailyCheckLogEntrys = GetQueryText("monitor/getDailyCheckLogEntrys.graphql");
                addDataImportLogEntry = GetQueryText("monitor/addDataImportLogEntry.graphql");
                getDataImportLogEntrys = GetQueryText("monitor/getDataImportLogEntrys.graphql");

                getOwnerTickets = RequestQueries.ticketOverviewFragment + GetQueryText("monitor/getOwnerTickets.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize MonitorQueries", "Api MonitorQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
