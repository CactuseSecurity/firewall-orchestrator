using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;
using FWO.DeviceAutoDiscovery;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the autodiscovery
	/// </summary>
    public class AutoDiscoverScheduler : SchedulerBase
    {
        private long? lastMgmtAlertId;

        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer AutoDiscoverTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<AutoDiscoverScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new AutoDiscoverScheduler(apiConnection, globalConfig);
        }
    
        private AutoDiscoverScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeAutodiscoveryConfigChanges)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionPartialUpdateHandler(config.ToArray());
            AutoDiscoverTimer.Interval = globalConfig.AutoDiscoverSleepTime * GlobalConst.kHoursToMilliseconds;
            StartScheduleTimer();
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
        {
            if (globalConfig.AutoDiscoverSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.AutoDiscoverStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddHours(globalConfig.AutoDiscoverSleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Autodiscover scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += AutoDiscover;
                ScheduleTimer.Elapsed += StartAutoDiscoverTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Autodiscover scheduler", "AutodiscoverScheduleTimer started.");
            }
        }

        private void StartAutoDiscoverTimer(object? _, ElapsedEventArgs __)
        {
            AutoDiscoverTimer.Stop();
            AutoDiscoverTimer = new();
            AutoDiscoverTimer.Elapsed += AutoDiscover;
            AutoDiscoverTimer.Interval = globalConfig.AutoDiscoverSleepTime * GlobalConst.kHoursToMilliseconds;
            AutoDiscoverTimer.AutoReset = true;
            AutoDiscoverTimer.Start();
            Log.WriteDebug("Autodiscover scheduler", "AutoDiscoverTimer started.");
        }

        private async void AutoDiscover(object? _, ElapsedEventArgs __)
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
                foreach (Management superManagement in managements.Where(x => x.DeviceType.CanBeSupermanager() || x.DeviceType.CanBeAutodiscovered(x)))
                {
                    try
                    {
                        AutoDiscoveryBase autodiscovery = new AutoDiscoveryBase(superManagement, apiConnection);

                        List<Management> diffList = await autodiscovery.Run();
                        List<ActionItem> actions = autodiscovery.ConvertToActions(diffList);
                        // List<ActionItem> actions = autodiscovery.ConvertToActions(await autodiscovery.Run());

                        int ChangeCounter = 0;

                        foreach (ActionItem action in actions)
                        {
                            if (action.ActionType == ActionCode.AddGatewayToNewManagement.ToString())
                            {
                                action.RefAlertId = lastMgmtAlertId;
                            }
                            action.AlertId = await SetAlert(action);
                            ChangeCounter++;
                        }
                        await AddLogEntry(0, globalConfig.GetText("scheduled_autodiscovery"),
                            ChangeCounter > 0 ? ChangeCounter + globalConfig.GetText("changes_found") : globalConfig.GetText("found_no_changes"),
                            GlobalConst.kAutodiscovery, superManagement.Id);
                    }
                    catch (Exception excMgm)
                    {
                        Log.WriteError("Autodiscovery", $"Ran into exception while auto-discovering management {superManagement.Name} (id: {superManagement.Id}) ", excMgm);
                        ActionItem actionException = new()
                        {
                            Number = 0,
                            ActionType = ActionCode.WaitForTempLoginFailureToPass.ToString(),
                            ManagementId = superManagement.Id,
                            Supermanager = superManagement.Name,
                            JsonData = excMgm.Message
                        };
                        await SetAlert(actionException);
                        await AddLogEntry(1, globalConfig.GetText("scheduled_autodiscovery"),
                            $"Ran into exception while handling management {superManagement.Name} (id: {superManagement.Id}): " + excMgm.Message,
                            GlobalConst.kAutodiscovery, superManagement.Id);
                    }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConst.kAutodiscovery}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to autodiscover\", description: \"{exc}\", alertCode: \"{AlertCode.Autodiscovery}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_autodiscovery"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kAutodiscovery);
            }
        }

        private async Task<long?> SetAlert(ActionItem action)
        {
            string title = "Supermanagement: " + action.Supermanager;
            lastMgmtAlertId = await SetAlert(title, action.ActionType ?? "", GlobalConst.kAutodiscovery, AlertCode.Autodiscovery,
                action.ManagementId, action.JsonData?.ToString(), action.DeviceId, action.RefAlertId, true);
            return lastMgmtAlertId;
        }
    }
}
