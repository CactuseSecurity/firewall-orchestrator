using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import of IP data per area
	/// </summary>
    public class ImportIpDataScheduler : SchedulerBase
    {
        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ImportIpDataTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportIpDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportIpDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportIpDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportIpDataConfigChanges)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler(config.ToArray());
            if (globalConfig.ImportSubnetDataSleepTime > 0)
            {
                ImportIpDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * GlobalConst.kHoursToMilliseconds;
                StartScheduleTimer();
            }
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
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
                    Log.WriteError("Import Area IP Data scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += ImportAreaIpData;
                ScheduleTimer.Elapsed += StartImportIpDataTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Import Area IP Data scheduler", "ImportIpDataScheduleTimer started.");
            }
        }

        private void StartImportIpDataTimer(object? _, ElapsedEventArgs __)
        {
            ImportIpDataTimer.Stop();
            ImportIpDataTimer = new();
            ImportIpDataTimer.Elapsed += ImportAreaIpData;
            ImportIpDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * GlobalConst.kHoursToMilliseconds;
            ImportIpDataTimer.AutoReset = true;
            ImportIpDataTimer.Start();
            Log.WriteDebug("Import Area IP Data scheduler", "ImportIpDataTimer started.");
        }

        private async void ImportAreaIpData(object? _, ElapsedEventArgs __)
        {
            try
            {
                AreaIpDataImport import = new AreaIpDataImport(apiConnection, globalConfig);
                if(!await import.Run())
                {
                    throw new Exception("Area IP Data Import failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area IP Data", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConst.kImportAreaSubnetData}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to import Area IP Data\", description: \"{exc}\", alertCode: \"{AlertCode.ImportAreaSubnetData}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_subnet_import"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportAreaSubnetData);
                await SetAlert("Import Area IP Data failed", exc.Message, GlobalConst.kImportAreaSubnetData, AlertCode.ImportAreaSubnetData);
            }
        }
    }
}
