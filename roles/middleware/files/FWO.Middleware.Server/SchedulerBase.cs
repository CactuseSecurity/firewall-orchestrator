using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Text.Json;
using System.Timers;

namespace FWO.Middleware.Server
{
 	/// <summary>
	/// Class handling the scheduler base processing
	/// </summary>
    public abstract class SchedulerBase
    {
		/// <summary>
		/// API connection
		/// </summary>
        protected readonly ApiConnection apiConnection;

		/// <summary>
		/// Global config
		/// </summary>
        protected GlobalConfig globalConfig;

		/// <summary>
		/// Global config change subscription
		/// </summary>
        protected GraphQlApiSubscription<List<ConfigItem>>? ConfigDataSubscription;

        /// <summary>
        /// Additional Data for Alerts
        /// </summary>
        protected struct AdditionalAlertData
        {
            /// <summary>
            /// Management Id
            /// </summary>
            public int? MgmtId {get; set;}
            /// <summary>
            /// Json Data
            /// </summary>
            public object? JsonData {get; set;}
            /// <summary>
            /// Device Id
            /// </summary>
            public int? DevId {get; set;}
            /// <summary>
            /// Reference on other Alert Id
            /// </summary>
            public long? RefAlertId {get; set;}
        }

		/// <summary>
        /// Schedule Timer
        /// </summary>
        protected System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer RecurringTimer = new();
        private readonly string SchedulerText;
        private readonly SchedulerInterval SchedulerInterval;
        private int SleepTime;


        /// <summary>
        /// Constructor starting the Schedule timer
        /// </summary>
        protected SchedulerBase(ApiConnection apiConnection, GlobalConfig globalConfig, string configDataSubscription, SchedulerInterval schedulerInterval, string schedulerName)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            ConfigDataSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnGlobalConfigChange, configDataSubscription);
            SchedulerText = "Scheduler-" + schedulerName;
            SchedulerInterval = schedulerInterval;
        }

        /// <summary>
        /// set scheduling timer from config values, to be overwritten for specific scheduler
        /// </summary>
        protected abstract void OnGlobalConfigChange(List<ConfigItem> config);

		/// <summary>
		/// subscription exception handling
		/// </summary>
        protected void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError(SchedulerText, "Api subscription lead to exception. Retry subscription.", exception);
            // Subscription will be restored if no exception is thrown here
        }

        /// <summary>
        /// define the processing to be done, to be overwritten for specific schedule
        /// </summary>
        protected abstract void Process(object? _, ElapsedEventArgs __);

        /// <summary>
        /// start the scheduling timer, to be called by specific scheduler
        /// </summary>
        protected void StartScheduleTimer(int sleepTime, DateTime startTime)
        {
            SleepTime = sleepTime;

            StopAllTimers();

            if (SleepTime > 0)
            {
                try
                {
                    ScheduleTimer.Elapsed += Process;
                    ScheduleTimer.Elapsed += StartRecurringTimer;
                    ScheduleTimer.Interval = (CalculateStartTime(startTime) - DateTime.Now).TotalMilliseconds;
                    ScheduleTimer.AutoReset = false;
                    ScheduleTimer.Start();
                    Log.WriteInfo(SchedulerText, "ScheduleTimer started.");
                }
                catch (Exception exception)
                {
                    Log.WriteError(SchedulerText, "Could not start ScheduleTimer.", exception);
                }
            }
        }

        private void StartRecurringTimer(object? _, ElapsedEventArgs __)
        {
            try
            {
                StopTimer(ref RecurringTimer, false);
                RecurringTimer.Elapsed += Process;
                RecurringTimer.Interval = SleepTimeToMilliseconds();
                RecurringTimer.AutoReset = true;
                RecurringTimer.Start();
                Log.WriteInfo(SchedulerText, "RecurringTimer started.");
            }
            catch (Exception exception)
            {
                Log.WriteError(SchedulerText, "Could not start RecurringTimer.", exception);
            }
        }

        /// <summary>
        /// Stop and dispose both timers to avoid handler accumulation/memory leaks.
        /// </summary>
        protected void StopAllTimers()
        {
            StopTimer(ref ScheduleTimer, true);
            StopTimer(ref RecurringTimer, false);
        }

        private void StopTimer(ref System.Timers.Timer timer, bool removeStartRecurringHandler)
        {
            try
            {
                timer.Stop();
                timer.Elapsed -= Process;

                if (removeStartRecurringHandler)
                {
                    timer.Elapsed -= StartRecurringTimer;
                }

                timer.Dispose();
            }
            catch (ObjectDisposedException)
            { 

            }

            timer = new();
        }

        private DateTime CalculateStartTime(DateTime startTime)
        {
            try
            {
                while (startTime < DateTime.Now)
                {
                    startTime = SchedulerInterval switch
                    {
                        SchedulerInterval.Days => startTime.AddDays(SleepTime),
                        SchedulerInterval.Hours => startTime.AddHours(SleepTime),
                        SchedulerInterval.Minutes => startTime.AddMinutes(SleepTime),
                        SchedulerInterval.Seconds => startTime.AddSeconds(SleepTime),
                        _ => throw new NotSupportedException($"Error: wrong time interval format:" + SchedulerInterval.ToString())
                    };
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(SchedulerText, "Could not calculate start time.", exception);
            }
            return startTime;
        }

        private int SleepTimeToMilliseconds()
        {
            return SchedulerInterval switch
            {
                SchedulerInterval.Days => SleepTime * GlobalConst.kDaysToMilliseconds,
                SchedulerInterval.Hours => SleepTime * GlobalConst.kHoursToMilliseconds,
                SchedulerInterval.Minutes => SleepTime * GlobalConst.kMinutesToMilliseconds,
                SchedulerInterval.Seconds => SleepTime * GlobalConst.kSecondsToMilliseconds,
                _ => throw new NotSupportedException($"Error: wrong time interval format:" + SchedulerInterval.ToString())
            };
        }

		/// <summary>
        /// Write Log and alert
        /// </summary>
        protected async Task LogErrorsWithAlert(int severity, string title, string source, AlertCode alertCode, Exception exc)
        {
            try
            {
                Log.WriteError(title, $"Ran into exception: ", exc);
                string titletext = $"Error encountered while trying {title}";
                await AddLogEntry(severity, title, globalConfig.GetText("ran_into_exception") + exc.Message, source);
                await SetAlert(title, titletext, source, alertCode);
            }
            catch (Exception exception)
            {
                Log.WriteError(title, $"something went really wrong", exception);
            }
       }

		/// <summary>
        /// Write Log to Database. Can be overwritten, if more than basic columns are to be filled
        /// </summary>
        protected virtual async Task AddLogEntry(int severity, string cause, string description, string source, int? mgmtId = null)
        {
            try
            {
                var Variables = new
                {
                    source = source,
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                    mgmId = mgmtId,
                    devId = (int?)null,
                    importId = (long?)null,
                    objectType = (string?)null,
                    objectName = (string?)null,
                    objectUid = (string?)null,
                    ruleUid = (string?)null,
                    ruleId = (long?)null
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addLogEntry, Variables)).ReturnIds;
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

		/// <summary>
		/// set an alert in error case
		/// </summary>
        protected async Task<long?> SetAlert(string title, string description, string source, AlertCode alertCode,
            AdditionalAlertData additionalAlertData = new(), bool compareDesc = false)
        {
            long? alertId = null;
            try
            {
                List<Alert> openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                var Variables = new
                {
                    source = source,
                    userId = 0,
                    title = title,
                    description = description,
                    mgmId = additionalAlertData.MgmtId,
                    devId = additionalAlertData.DevId,
                    alertCode = (int)alertCode,
                    jsonData = additionalAlertData.JsonData,
                    refAlert = additionalAlertData.RefAlertId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    alertId = returnIds[0].NewIdLong;
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == alertCode &&
                        (x.ManagementId == additionalAlertData.MgmtId || (x.ManagementId == null && additionalAlertData.MgmtId == null))
                        && (!compareDesc || x.Description == description));
                    if (existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                LogAlert(title, description, source, alertCode, additionalAlertData.MgmtId, additionalAlertData.JsonData, additionalAlertData.DevId);
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for {source}: ", exc);
                LogAlert(title, description, source, alertCode, additionalAlertData.MgmtId, additionalAlertData.JsonData, additionalAlertData.DevId);
            }
            return alertId;
        }

        private static void LogAlert(string title, string description, string source, AlertCode alertCode, int? mgmtId, object? JsonData, int? devId)
        {
            string? mgmtIdString = mgmtId?.ToString() ?? ""; 
            string? devIdString = devId?.ToString() ?? ""; 
            string jsonString = JsonData != null ? JsonSerializer.Serialize(JsonData) : ""; 
            Log.WriteAlert ($"source: \"{source}\"", $"userId: \"0\", title: \"{title}\", description: \"{description}\", " +
                $"mgmId: \"{mgmtIdString}\", devId: \"{devIdString}\", jsonData: \"{jsonString}\", alertCode: \"{alertCode}\"");
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
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for {alertId}: ", exception);
            }
        }
    }
}
