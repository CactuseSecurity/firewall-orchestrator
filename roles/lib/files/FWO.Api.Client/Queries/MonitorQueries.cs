using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class MonitorQueries : Queries
    {
        public static readonly string getLogEntrys;
        public static readonly string addUiLogEntry;
        public static readonly string getUiLogEntrys;
        public static readonly string getImportLogEntrys;
        public static readonly string addAlertEntry;
        public static readonly string getOpenAlerts;
        public static readonly string getAlertEntrys;
        public static readonly string acknowledgeAlert;
        public static readonly string subscribeAlertChanges;


        static MonitorQueries()
        {
            try
            {
                getLogEntrys = File.ReadAllText(QueryPath + "monitor/getLogEntrys.graphql");

                addUiLogEntry = File.ReadAllText(QueryPath + "monitor/addUiLogEntry.graphql");
                getUiLogEntrys = File.ReadAllText(QueryPath + "monitor/getUiLogEntrys.graphql");

                getImportLogEntrys = File.ReadAllText(QueryPath + "monitor/getImportLogEntrys.graphql");

                addAlertEntry = File.ReadAllText(QueryPath + "monitor/addAlertEntry.graphql");
                getOpenAlerts = File.ReadAllText(QueryPath + "monitor/getOpenAlerts.graphql");
                getAlertEntrys = File.ReadAllText(QueryPath + "monitor/getAlertEntrys.graphql");
                acknowledgeAlert = File.ReadAllText(QueryPath + "monitor/acknowledgeAlert.graphql");
                subscribeAlertChanges = File.ReadAllText(QueryPath + "monitor/subscribeAlertChanges.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize MonitorQueries", "Api MonitorQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
