using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
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
        private const string LogMessageTitle = GlobalConst.kAutodiscovery;

		/// <summary>
        /// Async Constructor needing the connection
        /// </summary>
        public static async Task<AutoDiscoverScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new AutoDiscoverScheduler(apiConnection, globalConfig);
        }
    
        private AutoDiscoverScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeAutodiscoveryConfigChanges, SchedulerInterval.Hours, "Autodiscover")
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            StartScheduleTimer(globalConfig.AutoDiscoverSleepTime, globalConfig.AutoDiscoverStartAt);
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
                foreach (Management superManagement in managements.Where(x => x.DeviceType.CanBeSupermanager() || x.DeviceType.CanBeAutodiscovered(x)))
                {
                    try
                    {
                        AutoDiscoveryBase autodiscovery = new (superManagement, apiConnection);

                        List<Management> diffList = await autodiscovery.Run();
                        List<ActionItem> actions = autodiscovery.ConvertToActions(diffList);

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
                        Log.WriteError(LogMessageTitle, $"Ran into exception while auto-discovering management {superManagement.Name} (id: {superManagement.Id}) ", excMgm);
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
                await LogErrorsWithAlert(1, LogMessageTitle, GlobalConst.kAutodiscovery, AlertCode.Autodiscovery, exc);
            }
        }

        private async Task<long?> SetAlert(ActionItem action)
        {
            string title = "Supermanagement: " + action.Supermanager;
            lastMgmtAlertId = await SetAlert(title, action.ActionType ?? "", GlobalConst.kAutodiscovery, AlertCode.Autodiscovery,
                new() { MgmtId = action.ManagementId, JsonData = action.JsonData?.ToString(), DevId = action.DeviceId, RefAlertId = action.RefAlertId }, true);
            return lastMgmtAlertId;
        }
    }
}
