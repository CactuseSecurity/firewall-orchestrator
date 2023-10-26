using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import of app data
	/// </summary>
    public class ImportAppDataScheduler
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private long? lastMgmtAlertId;
        private List<Alert> openAlerts = new List<Alert>();

        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ImportAppDataTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportAppDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportAppDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportAppDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfig_OnChange;
            startScheduleTimer();
        }

        private void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            ScheduleTimer.Stop();
            if(globalConfig.ImportAppDataSleepTime > 0)
            {
                ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * 3600000; // convert hours to milliseconds
                startScheduleTimer();
            }
        }

        private void startScheduleTimer()
        {
            if (globalConfig.ImportAppDataSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.ImportAppDataStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddHours(globalConfig.ImportAppDataSleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Import App Data scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += ImportAppData;
                ScheduleTimer.Elapsed += StartImportAppDataTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Import App Data scheduler", "ImportAppDataScheduleTimer started.");
            }
        }

        private void StartImportAppDataTimer(object? _, ElapsedEventArgs __)
        {
            ImportAppDataTimer.Stop();
            ImportAppDataTimer = new();
            ImportAppDataTimer.Elapsed += ImportAppData;
            ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * 3600000;  // convert hours to milliseconds
            ImportAppDataTimer.AutoReset = true;
            ImportAppDataTimer.Start();
            Log.WriteDebug("Import App Data scheduler", "ImportAppDataTimer started.");
        }

        private async void ImportAppData(object? _, ElapsedEventArgs __)
        {
            try
            {
                openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);

                ImportAppData import = new ImportAppData(apiConnection, globalConfig);
                if(!await import.Run())
                {
                    throw new Exception("Import App Data failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConfig.kImportAppData}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to import App Data\", description: \"{exc}\", alertCode: \"{AlertCode.ImportAppData}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_app_import"), globalConfig.GetText("ran_into_exception") + exc.Message);
            }
        }

        private async Task setAlert(string title, string description)
        {
            try
            {
                var Variables = new
                {
                    source = GlobalConfig.kImportAppData,
                    userId = 0,
                    title = title,
                    description = description,
                    alertCode = (int)AlertCode.ImportAppData
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == AlertCode.ImportAppData);
                    if(existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                Log.WriteAlert ($"source: \"{GlobalConfig.kImportAppData}\"", 
                    $"userId: \"0\", title: \"{title}\", description: \"{description}\", alertCode: \"{AlertCode.ImportAppData.ToString()}\"");
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for import App Data: ", exc);
            }
        }

        private async Task AcknowledgeAlert(long alertId)
        {
            try
            {
                var Variables = new
                {
                    id = alertId,
                    ackUser = 0,
                    ackTime = DateTime.Now
                };
                await apiConnection.SendQueryAsync<ReturnId>(MonitorQueries.acknowledgeAlert, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for import App Data: ", exception);
            }
        }

        private async Task AddLogEntry(int severity, string cause, string description)
        {
            try
            {
                var Variables = new
                {
                    source = GlobalConfig.kImportAppData,
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                    mgmId = (int?)null,
                    devId = (int?)null,
                    importId = (long?)null,
                    objectType = (string?)null,
                    objectName = (string?)null,
                    objectUid = (string?)null,
                    ruleUid = (string?)null,
                    ruleId = (long?)null
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}
