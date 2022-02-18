using FWO.ApiClient;
using FWO.Api.Data;
using FWO.Config.Api;
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
        private long lastMgmtAlertId = 0;

    
        public AutoDiscoverScheduler(APIConnection apiConnection)
        {
            this.apiConnection = apiConnection;
    
            config = new ConfigDbAccess(apiConnection);
            try
            {
                autoDiscoverSleepTime = config.Get<int>(GlobalConfig.kAutoDiscoverSleepTime);
            }
            catch (KeyNotFoundException) {}

            if(autoDiscoverSleepTime > 0)
            {
                System.Timers.Timer checkScheduleTimer = new();
                checkScheduleTimer.Elapsed += AutoDiscover;
                checkScheduleTimer.Interval = autoDiscoverSleepTime * 60000; // 3600000;  // convert hours to milliseconds
                checkScheduleTimer.AutoReset = true;
                checkScheduleTimer.Start();
            }
        }

        private async void AutoDiscover(object? _, ElapsedEventArgs __)
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(FWO.ApiClient.Queries.DeviceQueries.getManagementsDetails);
                foreach(Management superManagement in managements.Where(x => x.DeviceType.CanBeSupermanager()))
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.ApiClient.Queries.MonitorQueries.addAlert, Variables)).ReturnIds;
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.ApiClient.Queries.MonitorQueries.addAutodiscoveryLogEntry, Variables)).ReturnIds;
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
