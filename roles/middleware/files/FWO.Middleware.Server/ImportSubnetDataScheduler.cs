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
	/// Class handling the scheduler for the import of subnet data
	/// </summary>
    public class ImportSubnetDataScheduler : SchedulerBase
    {
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
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportSubnetDataConfigChanges)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionPartialUpdateHandler(config.ToArray());
            if (globalConfig.ImportSubnetDataSleepTime > 0)
            {
                ImportSubnetDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * GlobalConst.kHoursToMilliseconds;
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
            ImportSubnetDataTimer.Interval = globalConfig.ImportSubnetDataSleepTime * GlobalConst.kHoursToMilliseconds;
            ImportSubnetDataTimer.AutoReset = true;
            ImportSubnetDataTimer.Start();
            Log.WriteDebug("Import Area Subnet Data scheduler", "ImportSubnetDataTimer started.");
        }

        private async void ImportAreaSubnetData(object? _, ElapsedEventArgs __)
        {
            try
            {
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
                await AddLogEntry(1, globalConfig.GetText("scheduled_subnet_import"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportAreaSubnetData);
                await SetAlert("Import Area Subnet Data failed", exc.Message, GlobalConst.kImportAreaSubnetData, AlertCode.ImportAreaSubnetData);
            }
        }
    }
}
