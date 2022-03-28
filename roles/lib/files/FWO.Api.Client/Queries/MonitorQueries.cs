using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class MonitorQueries : Queries
    {
        public static readonly string addLogEntry;
        public static readonly string getLogEntrys;
        public static readonly string addUiLogEntry;
        public static readonly string getUiLogEntrys;
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


        static MonitorQueries()
        {
            try
            {
                addLogEntry = File.ReadAllText(QueryPath + "monitor/addLogEntry.graphql");
                getLogEntrys = File.ReadAllText(QueryPath + "monitor/getLogEntrys.graphql");

                addUiLogEntry = File.ReadAllText(QueryPath + "monitor/addUiLogEntry.graphql");
                getUiLogEntrys = File.ReadAllText(QueryPath + "monitor/getUiLogEntrys.graphql");

                getImportLogEntrys = File.ReadAllText(QueryPath + "monitor/getImportLogEntrys.graphql");

                addAlert = File.ReadAllText(QueryPath + "monitor/addAlert.graphql");
                getOpenAlerts = File.ReadAllText(QueryPath + "monitor/getOpenAlerts.graphql");
                getAlerts = File.ReadAllText(QueryPath + "monitor/getAlerts.graphql");
                getAlertById = File.ReadAllText(QueryPath + "monitor/getAlertById.graphql");
                acknowledgeAlert = File.ReadAllText(QueryPath + "monitor/acknowledgeAlert.graphql");
                subscribeAlertChanges = File.ReadAllText(QueryPath + "monitor/subscribeAlertChanges.graphql");

                addAutodiscoveryLogEntry = File.ReadAllText(QueryPath + "monitor/addAutodiscoveryLogEntry.graphql");
                getAutodiscoveryLogEntrys = File.ReadAllText(QueryPath + "monitor/getAutodiscoveryLogEntrys.graphql");
                getDailyCheckLogEntrys = File.ReadAllText(QueryPath + "monitor/getDailyCheckLogEntrys.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize MonitorQueries", "Api MonitorQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
