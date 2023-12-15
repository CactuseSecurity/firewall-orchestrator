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
	/// Class handling the scheduler for the import of subnet data
	/// </summary>
    public class ImportSubnetDataScheduler
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private List<Alert> openAlerts = new List<Alert>();

        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ImportSubnetDataTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportSubnetDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportSubnetDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportSubnetDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfig_OnChange;
            StartScheduleTimer();
        }

        private void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            ScheduleTimer.Stop();
            if (globalConfig.ImportSubnetDataSleepTime > 0)
            {
                ImportSubnetDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * 3600000; // convert hours to milliseconds
                StartScheduleTimer();
            }
        }

        private void StartScheduleTimer()
        {
            if (globalConfig.ImportSubnetDataSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.ImportSubnetDataStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddHours(globalConfig.ImportSubnetDataSleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Import Area Subnet Data scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += ImportAreaSubnetData;
                ScheduleTimer.Elapsed += StartImportSubnetDataTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Import Area Subnet Data scheduler", "ImportSubnetDataScheduleTimer started.");
            }
        }

        private void StartImportSubnetDataTimer(object? _, ElapsedEventArgs __)
        {
            ImportSubnetDataTimer.Stop();
            ImportSubnetDataTimer = new();
            ImportSubnetDataTimer.Elapsed += ImportAreaSubnetData;
            ImportSubnetDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * 3600000;  // convert hours to milliseconds
            ImportSubnetDataTimer.AutoReset = true;
            ImportSubnetDataTimer.Start();
            Log.WriteDebug("Import Area Subnet Data scheduler", "ImportSubnetDataTimer started.");
        }

        private async void ImportAreaSubnetData(object? _, ElapsedEventArgs __)
        {
            try
            {
                openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                AreaSubnetDataImport import = new AreaSubnetDataImport(apiConnection, globalConfig);
                if(!await import.Run())
                {
                    throw new Exception("Area Subnet Import failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConst.kImportAreaSubnetData}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to import Area Subnet Data\", description: \"{exc}\", alertCode: \"{AlertCode.ImportAreaSubnetData}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_subnet_import"), globalConfig.GetText("ran_into_exception") + exc.Message);
                await SetAlert("Import Area Subnet Data failed", exc.Message);
            }
        }

        private async Task SetAlert(string title, string description)
        {
            try
            {
                var Variables = new
                {
                    source = GlobalConst.kImportAreaSubnetData,
                    userId = 0,
                    title = title,
                    description = description,
                    alertCode = (int)AlertCode.ImportAreaSubnetData
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == AlertCode.ImportAreaSubnetData);
                    if(existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                Log.WriteAlert ($"source: \"{GlobalConst.kImportAreaSubnetData}\"", 
                    $"userId: \"0\", title: \"{title}\", description: \"{description}\", alertCode: \"{AlertCode.ImportAreaSubnetData.ToString()}\"");
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for import Area Subnet Data: ", exc);
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
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for import Area Subnet Data: ", exception);
            }
        }

        private async Task AddLogEntry(int severity, string cause, string description)
        {
            try
            {
                var Variables = new
                {
                    source = GlobalConst.kImportAreaSubnetData,
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
