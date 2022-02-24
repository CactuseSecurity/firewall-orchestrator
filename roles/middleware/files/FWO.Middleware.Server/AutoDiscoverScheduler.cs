using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;
using FWO.DeviceAutoDiscovery;

namespace FWO.Middleware.Server
{
    public class AutoDiscoverScheduler
    {
        private readonly APIConnection apiConnection;
        private ConfigDbAccess config;
        private int autoDiscoverSleepTime = GlobalConfig.kDefaultInitAutoDiscoverSleepTime; // in hours
        private string autoDiscoverStartAt = DateTime.Now.TimeOfDay.ToString();
        private long? lastMgmtAlertId;
        private readonly ApiSubscription<List<ConfigItem>> configChangeSubscription;

        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer AutoDiscoverTimer = new();

    
        public AutoDiscoverScheduler(APIConnection apiConnection)
        {
            this.apiConnection = apiConnection;
    
            config = new ConfigDbAccess(apiConnection);
            try
            {
                autoDiscoverSleepTime = config.Get<int>(GlobalConfig.kAutoDiscoverSleepTime);
                autoDiscoverStartAt = config.Get<string>(GlobalConfig.kAutoDiscoverStartAt);
            }
            catch (KeyNotFoundException) {}
            
            configChangeSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnConfigUpdate, ConfigQueries.subscribeAutodiscoveryConfigChanges);

            startScheduleTimer();
        }

        public void startScheduleTimer()
        {
            if(autoDiscoverSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = Convert.ToDateTime(autoDiscoverStartAt);
                    while(startTime < DateTime.Now)
                    {
                        startTime = startTime.AddMinutes(autoDiscoverSleepTime); // todo:hours!
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Autodiscover scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;
            
                ScheduleTimer = new();
                ScheduleTimer.Elapsed += StartAutoDiscoverTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Autodiscover scheduler", "ScheduleTimer started.");
            }
        }

        private void StartAutoDiscoverTimer(object? _, ElapsedEventArgs __)
        {
            AutoDiscoverTimer.Stop();
            AutoDiscoverTimer = new();
            AutoDiscoverTimer.Elapsed += AutoDiscover;
            AutoDiscoverTimer.Interval = autoDiscoverSleepTime * 3600000;  // convert hours to milliseconds
            AutoDiscoverTimer.AutoReset = true;
            AutoDiscoverTimer.Start();
            Log.WriteDebug("Autodiscover scheduler", "AutoDiscoverTimer started.");
        }

        private void OnConfigUpdate(List<ConfigItem> configItems)
        {
            if(configItems.Count() > 0 && configItems[0] != null)
            {
                ConfigItem? confItem = configItems[0];
                if (confItem.Value != null && confItem.Value != "")
                {
                    autoDiscoverSleepTime = Int32.Parse(confItem.Value);
                    AutoDiscoverTimer.Interval = autoDiscoverSleepTime * 3600000; // convert hours to milliseconds
                    ScheduleTimer.Stop();
                    startScheduleTimer();
                }
            }
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError("Autodiscover scheduler", "Api subscription lead to exception. Retry subscription.", exception);
        }

        private async void AutoDiscover(object? _, ElapsedEventArgs __)
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
                foreach(Management superManagement in managements.Where(x => x.DeviceType.CanBeSupermanager() || x.DeviceType.CanBeAutodiscovered(x)))
                {
                    AutoDiscoveryBase autodiscovery = new AutoDiscoveryBase(superManagement, apiConnection);
                    List<ActionItem> actions = autodiscovery.ConvertToActions(await autodiscovery.Run());

                    int ChangeCounter = 0;

                    foreach(ActionItem action in actions)
                    {
                        if(action.ActionType == ActionCode.AddGatewayToNewManagement.ToString())
                        {
                            action.RefAlertId = lastMgmtAlertId;
                        }
                        await setAlert(action);
                        ChangeCounter++;
                    }
                    await AddAutoDiscoverLogEntry(0, "Scheduled Autodiscovery", superManagement.Name + (ChangeCounter > 0 ? $": found {ChangeCounter} changes" : ": found no change"));
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Autodiscovery", $"Ran into exception: ", exc);
            }
        }

        public async Task setAlert(ActionItem action)
        {
            try
            {
                var Variables = new
                {
                    source = "autodiscovery",
                    userId = 0,
                    title = action.Supermanager,
                    description = action.ActionType,
                    mgmId = action.ManagementId,
                    devId = action.DeviceId,
                    jsonData = action.JsonData,
                    refAlert = action.RefAlertId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    if(action.ActionType == ActionCode.AddManagement.ToString())
                    {
                        lastMgmtAlertId = returnIds[0].NewId;
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for autodiscovery: ", exc);
            }
        }

        public async Task AddAutoDiscoverLogEntry(int severity, string cause, string description)
        {
            try
            {
                var Variables = new
                {
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAutodiscoveryLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}
