﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private long? lastMgmtAlertId;
        private List<Alert> openAlerts = new List<Alert>();

        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer AutoDiscoverTimer = new();

        public static async Task<AutoDiscoverScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new AutoDiscoverScheduler(apiConnection, globalConfig);
        }
    
        private AutoDiscoverScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfig_OnChange;
            startScheduleTimer();
        }

        private void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            AutoDiscoverTimer.Interval = globalConfig.AutoDiscoverSleepTime * 3600000; // convert hours to milliseconds
            ScheduleTimer.Stop();
            startScheduleTimer();
        }

        public void startScheduleTimer()
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
            AutoDiscoverTimer.Interval = globalConfig.AutoDiscoverSleepTime * 3600000;  // convert hours to milliseconds
            AutoDiscoverTimer.AutoReset = true;
            AutoDiscoverTimer.Start();
            Log.WriteDebug("Autodiscover scheduler", "AutoDiscoverTimer started.");
        }

        private async void AutoDiscover(object? _, ElapsedEventArgs __)
        {
            try
            {
                openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
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
                            action.AlertId = await setAlert(action);
                            ChangeCounter++;
                        }
                        await AddAutoDiscoverLogEntry(0, globalConfig.GetText("scheduled_autodiscovery"), (ChangeCounter > 0 ? ChangeCounter + globalConfig.GetText("changes_found") : globalConfig.GetText("found_no_changes")), superManagement.Id);
                    }
                    catch (Exception excMgm)
                    {
                        Log.WriteError("Autodiscovery", $"Ran into exception while auto-discovering management {superManagement.Name} (id: {superManagement.Id}) ", excMgm);
                        ActionItem actionException = new ActionItem();
                        actionException.Number = 0;
                        actionException.ActionType = ActionCode.WaitForTempLoginFailureToPass.ToString();
                        actionException.ManagementId = superManagement.Id;
                        actionException.Supermanager = superManagement.Name;
                        actionException.JsonData = excMgm.Message;
                        await setAlert(actionException);
                        await AddAutoDiscoverLogEntry(1, globalConfig.GetText("scheduled_autodiscovery"), $"Ran into exception while handling management {superManagement.Name} (id: {superManagement.Id}): " + excMgm.Message, superManagement.Id);
                    }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConfig.kAutodiscovery}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to autodiscover\", description: \"{exc}\", alertCode: \"{AlertCode.Autodiscovery}\"");
                await AddAutoDiscoverLogEntry(1, globalConfig.GetText("scheduled_autodiscovery"), globalConfig.GetText("ran_into_exception") + exc.Message);
            }
        }

        public async Task<long?> setAlert(ActionItem action)
        {
            long? alertId = null;
            try
            {
                string title = "Supermanagement: " + action.Supermanager;
                Log.WriteAlert($"source: \"{GlobalConfig.kAutodiscovery}\"",
                    $"userId: \"0\", title: \"{title}\", type: \"{action.ActionType}\", " +
                    $"mgmId: \"{action.ManagementId}\", devId: \"{action.DeviceId}\", jsonData: \"{action.JsonData?.ToString()}\", refAlert: \"{action.RefAlertId}\", alertCode: \"{AlertCode.Autodiscovery}\"");
                var Variables = new
                {
                    source = GlobalConfig.kAutodiscovery,
                    userId = 0,
                    title = title,
                    description = action.ActionType,
                    mgmId = action.ManagementId,
                    devId = action.DeviceId,
                    jsonData = action.JsonData,
                    refAlert = action.RefAlertId,
                    alertCode = (int)AlertCode.Autodiscovery
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    alertId = returnIds[0].NewId;
                    if (action.ActionType == ActionCode.AddManagement.ToString())
                    {
                        lastMgmtAlertId = alertId;
                    }
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == AlertCode.Autodiscovery
                                && x.Description == action.ActionType && x.ManagementId == action.ManagementId);
                    if (existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for autodiscovery: ", exc);
            }
            return alertId;
        }

        public async Task AcknowledgeAlert(long alertId)
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
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for autodiscovery: ", exception);
            }
        }

        public async Task AddAutoDiscoverLogEntry(int severity, string cause, string description, int? mgmtId = null)
        {
            try
            {
                var Variables = new
                {
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                    mgmId = mgmtId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAutodiscoveryLogEntry, Variables)).ReturnIds;
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
