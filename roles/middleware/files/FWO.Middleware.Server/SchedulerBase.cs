using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Services;
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
            if (SleepTime > 0)
            {
                try
                {
                    // Dispose old timer if existant
                    ScheduleTimer.Stop();
                    ScheduleTimer.Elapsed -= Process;
                    ScheduleTimer.Dispose();

                    ScheduleTimer = new();
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
                // Dispose old timer if existant
                RecurringTimer.Stop();
                RecurringTimer.Elapsed -= Process;
                RecurringTimer.Dispose();

                RecurringTimer = new();
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
    }
}
